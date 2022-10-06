using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace HealthCalculator;

public static class Program
{
	/// <summary>
	/// Main program entry point.
	/// </summary>
	public static void Main()
	{
		// Indicate program is running.
		Console.WriteLine("Starting...");
		
		// Start the business logic prototype.
		HealthinessCalculator();
		
		// Effectively pause to hold the results on screen... press any key to close program.
		_ = Console.ReadKey();
	}

	/// <summary>
	/// Prototype calculator for determining the body mass index and weight status based on user input.
	/// </summary>
	public static void HealthinessCalculator()
	{
		Sex? sex = null;
		while(sex is null or not (Sex.Male or Sex.Female))
		{
			sex = GetInputUntilValid("Enter your sex: ", ParseEnum.Parse<Sex>, "Input was invalid.");
			if(sex is Sex.Intersex)
			{
				Console.WriteLine("Intersexes are not supported in the current version, sorry!");
			}
		}
		double mass   = GetInputUntilValid<double>("Enter your mass (kg): ",  s => double.TryParse(s, out double parsed) ? parsed : default, "Input was invalid.");
		double height = GetInputUntilValid<double>("Enter Your height (m): ", s => double.TryParse(s, out double parsed) ? parsed : default, "Input was invalid.");
		double bmi = mass / (height * height);
		Console.WriteLine("Your BMI is: " + Math.Round(bmi, 2));
		WeightStatus weightStatus = GetWeightStatus(sex.Value, bmi);
		Console.WriteLine("Weight Status: " + weightStatus.ToString());
	}
	
#pragma warning disable RCS1016
	private static WeightStatus GetWeightStatus(Sex sex, double bmi) => sex switch
	{
		Sex.Male => bmi switch
		{
			< 18.5f => WeightStatus.Underweight,
			>= 18.5 and < 25f => WeightStatus.HealthyWeight,
			>= 25f and < 30f => WeightStatus.Overweight,
			_ => WeightStatus.Obese
		},
		Sex.Female => bmi switch
		{
			// Formula is not different for males and females, but you could put wrong values here if you really want.
			< 18.5f => WeightStatus.Underweight,
			>= 18.5 and < 25f => WeightStatus.HealthyWeight,
			>= 25f and < 30f => WeightStatus.Overweight,
			_ => WeightStatus.Obese
		},
		_ => throw new NotImplementedException(),
	};
#pragma warning restore RCS1016

	private static T GetInputUntilValid<T>(string? prompt, Func<string?, T?> attempt, string? errorMessage)
		where T : struct
	{
		T? t = default;
		while(t is null)
		{
			if(prompt is not null)
			{
				Console.WriteLine(prompt);
			}
			t = attempt(Console.ReadLine());
			if(t is null)
			{
				Console.WriteLine(errorMessage);
			}
		}
		return t.Value;
	}
}

/// <summary>
/// Represents a human's sex, including intersex disorders.
/// </summary>
public enum Sex
{
	[Names("Male", "M")]
	[Names("male", "m")]
	Male,
	[Names("Female", "F")]
	[Names("female", "f")]
	Female,
	[Names("Intersex", "I")]
	[Names("intersex", "i")]
	Intersex
}

/// <summary>
/// Describes the healthiness of a human's BMI, according to the CDC.
/// </summary>
public enum WeightStatus
{
	Underweight,
	HealthyWeight,
	Overweight,
	Obese,
}

/// <summary>
/// Gives an enum field any amount of correlated string values.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class NamesAttribute : Attribute
{
	public string[] Names { get; init; }

	public NamesAttribute(params string[] names)
	{
		if (names?.Any(x => x is null) is not false)
		{
			throw new ArgumentException($"{nameof(names)} has a null element.");
		}
		Names = names;
	}
}

/// <summary>
/// Handles parsing an enum with NamesAttributes on its fields.
/// </summary>
public static class ParseEnum
{
	/// <summary>
	/// Parses a string into an enum.
	/// </summary>
	/// <typeparam name="TEnum">Type of Enum to parse.</typeparam>
	/// <param name="value">The value to parse.</param>
	public static TEnum? Parse<TEnum>(string? value) where TEnum : struct => value is not null ? ParseEnumImpl<TEnum>.Values.GetValueOrDefault(value, null) : null;
	
	/// <summary>
	/// Tries to parse a string into an enum using the dumb pattern.
	/// </summary>
	/// <typeparam name="TEnum">Type of Enum to parse.</typeparam>
	/// <param name="value">The value to parse.</param>
	/// <param name="result">The newly parsed value.</param>
	public static bool TryParse<TEnum>(string? value, [NotNullWhen(true)] out TEnum? result) where TEnum : struct
	{
		if(value is null)
		{
			result = null;
			return false;
		}
		return ParseEnumImpl<TEnum>.Values.TryGetValue(value, out result);
	}

	/// <summary>
	/// Caches the string correlations in a dictionary.
	/// </summary>
	/// <typeparam name="TEnum">Type of Enum to parse.</typeparam>
	private static class ParseEnumImpl<TEnum> where TEnum : struct
	{
		// Stores correlations between values to parse and the proper result to return.
		public static readonly Dictionary<string, TEnum?> Values = new();

		static ParseEnumImpl()
		{
			// Get all fields on the enum and couple each one alongside all of its NamesAttributes.
			IEnumerable<(FieldInfo Value, IEnumerable<NamesAttribute> ManyNames)> manyNamedFieldInfos = typeof(TEnum)
				.GetFields()
				.Select(fieldInfo =>
					( 
						Value: fieldInfo, 
						ManyNames: fieldInfo.GetCustomAttributes(typeof(NamesAttribute), false).Cast<NamesAttribute>() 
					));

			// Flatten each field's list of NamesAttributes into a list of strings.
			IEnumerable<(FieldInfo Value, string Name)> degrouped = manyNamedFieldInfos.SelectMany<(FieldInfo Field, IEnumerable<NamesAttribute> ManyNames), string, (FieldInfo Field, string Name)>
			(
				collectionSelector: ((FieldInfo Field, IEnumerable<NamesAttribute> ManyNames) ManyNamedField) => ManyNamedField.ManyNames.SelectMany(y => y.Names),
				resultSelector:     ((FieldInfo Field, IEnumerable<NamesAttribute> ManyNames) ManyNamedField, string Name) => (ManyNamedField.Field, Name)
			);
			
			// Convert the flattened list of fields and their associated names into an even flatter dictionary.
			Values = degrouped.ToDictionary
			(
				keySelector:     namedFieldInfo => namedFieldInfo.Name, 
				elementSelector: namedFieldInfo => (TEnum?)namedFieldInfo.Value.GetValue(null)
			);
		}
	}
}
