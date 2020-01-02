using System;
using System.Collections.Generic;
using System.Linq;
using Quaver.API.Enums;
using Quaver.API.Helpers;
using Quaver.API.Maps.Structures;

namespace Quaver.API.Maps.Processors.Patterns
{
    public class PatternAnalyzer
    {
        /// <summary>
        ///     The current version of the pattern analyzer
        /// </summary>
        public static string Version = "0.0.1";

        /// <summary>
        ///     The map being analyzed for patterns
        /// </summary>
        private Qua Map { get; }

        /// <summary>
        ///     Modifiers that are applied to the map
        /// </summary>
        private ModIdentifier Mods { get; }

        /// <summary>
        ///     The speed of the map based on the activated modifiers
        /// </summary>
        private float Rate => ModHelper.GetRateFromMods(Mods);

        /// <summary>
        ///     The patterns that were detected within the map
        /// </summary>
        public List<PatternInfo> DetectedPatterns { get; } = new List<PatternInfo>();

        /// <summary>
        ///     The currently analyzed stream pattern
        /// </summary>
        private List<HitObjectInfo> CurrentStreamPattern { get; } = new List<HitObjectInfo>();

        /// <summary>
        ///     The minimum BPM required to be considered a stream pattern
        /// </summary>
        public const float MIN_STREAM_BPM = 120f;

        /// <summary>
        ///     The minimum amount of objects required to be considered a stream pattern
        /// </summary>
        private const int MIN_STREAM_OBJECTS = 4;

        /// <summary>
        ///     The total length of stream patterns within the map
        /// </summary>
        public float TotalStreamLength { get; private set; }

        /// <summary>
        ///     The total amount of "chordstreams"
        /// </summary>
        public int TotalChordStreamCount => CountJumpStream + CountHandStream + CountQuadStream + CountFivePlusStream;

        /// <summary>
        ///     The total "jumps" within the stream patterns
        /// </summary>
        public int CountJumpStream { get; private set; }

        /// <summary>
        ///     The total "hands" within the stream patterns
        /// </summary>
        public int CountHandStream { get; private set; }

        /// <summary>
        ///     The total "quads" within the stream patterns
        /// </summary>
        public int CountQuadStream { get; private set; }

        /// <summary>
        ///     The total amount of five+ chords within the stream patterns
        /// </summary>
        public int CountFivePlusStream { get; private set; }

        /// <summary>
        ///     The percentage of skillsets that make up the entire map
        /// </summary>
        public Dictionary<Skillset, float> SkillsetPercentages { get; private set; }

        /// <summary>
        /// </summary>
        /// <param name="map"></param>
        /// <param name="mods"></param>
        public PatternAnalyzer(Qua map, ModIdentifier mods = 0)
        {
            Map = map;
            Mods = mods;

            Analyze();

            Console.WriteLine("");
            Console.WriteLine($"Analysis for: " + Map);
            Console.WriteLine($"Detected Pattern Count: {DetectedPatterns.Count}");
            Console.WriteLine();
            Console.WriteLine($"Total Stream length: {TotalStreamLength} ms");
            Console.WriteLine($"'Jump' Stream Count: {CountJumpStream}");
            Console.WriteLine($"'Hand' Stream Count: {CountHandStream}");
            Console.WriteLine($"'Quad' Stream Count: {CountQuadStream}");
            // Console.WriteLine($"'5+' Stream Count: {CountFivePlusStream}");

            Console.WriteLine();

            Console.WriteLine("---- Skillset Breakdown ----");
            foreach (var skillset in SkillsetPercentages)
                Console.WriteLine($"{skillset.Key}: {skillset.Value:0.00}%");
        }

        /// <summary>
        ///     Performs a pattern analysis on the map
        /// </summary>
        private void Analyze()
        {
            if (Map.HitObjects.Count == 0)
                return;

            for (var i = 1; i < Map.HitObjects.Count; i++)
            {
                var hitObject = Map.HitObjects[i];
                var previousObject = Map.HitObjects[i - 1];

                AnalyzeStreamPattern(hitObject, previousObject);
            }

            FinalizeAnalysis();
        }

        /// <summary>
        /// </summary>
        /// <param name="hitObject"></param>
        /// <param name="previousObject"></param>
        private void AnalyzeStreamPattern(HitObjectInfo hitObject, HitObjectInfo previousObject)
        {
            var timeDiff = Math.Abs(hitObject.StartTime / Rate - previousObject.StartTime / Rate);
            var isMapLastObject = hitObject == Map.HitObjects.Last();

            // - The amount of time passed between the two objects exceed what is considered a stream pattern
            // - Objects in the same lane are not considered stream patterns
            if (timeDiff > 60000 / MIN_STREAM_BPM / 4 || hitObject.Lane == previousObject.Lane)
            {
                // The pervious object was still apart of the pattern, so add it to the list
                if (CurrentStreamPattern.Count != 0)
                    CurrentStreamPattern.Add(previousObject);

                FinalizeStreamPattern();
                return;
            }

            // Previous object is apart of the pattern
            CurrentStreamPattern.Add(previousObject);

            // The last object of the map, so add the current object.
            if (isMapLastObject)
            {
                CurrentStreamPattern.Add(hitObject);
                FinalizeStreamPattern();
            }
        }

        /// <summary>
        ///     Differentiates between jumpstreams, handstreams, trills, and jumptrills
        /// </summary>
        private void FinalizeStreamPattern()
        {
            if (CurrentStreamPattern.Count < MIN_STREAM_OBJECTS)
            {
                CurrentStreamPattern.Clear();
                return;
            }

            var pattern = new PatternInfo(PatternType.Stream, Mods, new List<HitObjectInfo>(CurrentStreamPattern));
            DetectedPatterns.Add(pattern);

            CurrentStreamPattern.Clear();
        }

        /// <summary>
        ///     Takes the detected patterns and determines the most common skillsets within the map
        /// </summary>
        private void FinalizeAnalysis()
        {
            var groups = DetectedPatterns.GroupBy(x => x.Type);

            foreach (var group in groups)
            {
                switch (group.Key)
                {
                    case PatternType.Stream:
                        CalculateStreamPatternWeight(group.ToList());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            DetermineMapSkillsets();
        }

        /// <summary>
        ///     Calculates the amount of weight the stream pattern types account for the map
        /// </summary>
        /// <param name="patterns"></param>
        private void CalculateStreamPatternWeight(List<PatternInfo> patterns)
        {
            foreach (var pattern in patterns)
            {
                TotalStreamLength += pattern.Length;
                CountJumpStream += pattern.JumpChordCount;
                CountHandStream += pattern.HandChordCount;
                CountQuadStream += pattern.QuadChordCount;
                CountFivePlusStream += pattern.FivePlusChordCount;
            }
        }

        /// <summary>
        ///     Determines the most common skillsets in the map
        /// </summary>
        private void DetermineMapSkillsets()
        {
            var streamPercentage = TotalStreamLength / (Map.Length / Rate) * 100;

            // Calc JS %
            var jumpstreamPercent = 0f;
            var relativeJumpstreamPercent = 0f;

            if (CountJumpStream != 0)
            {
                jumpstreamPercent = (float) CountJumpStream / TotalChordStreamCount * 100 / (100 / streamPercentage);
                relativeJumpstreamPercent = jumpstreamPercent / streamPercentage * 100;
            }

            // Calc HS %
            var handStreamPercent = 0f;
            var relativeHandstreamPercent = 0f;

            if (CountHandStream != 0)
            {
                handStreamPercent = (float) CountHandStream / TotalChordStreamCount * 100 / (100 / streamPercentage);
                relativeHandstreamPercent = handStreamPercent / streamPercentage * 100;
            }

            // Calc QS %
            var quadStreamPercent = 0f;
            var relativeQuadstreamPercent = 0f;

            if (CountQuadStream != 0)
            {
                quadStreamPercent = (float) CountQuadStream / TotalChordStreamCount * 100 / (100 / streamPercentage);
                relativeQuadstreamPercent = quadStreamPercent / streamPercentage * 100;
            }

            SkillsetPercentages = new Dictionary<Skillset, float>
            {
                {Skillset.Stream, streamPercentage},
                {Skillset.TotalJumpstream, jumpstreamPercent},
                {Skillset.RelativeJumpstream, relativeJumpstreamPercent},
                {Skillset.TotalHandstream, handStreamPercent},
                {Skillset.RelativeHandstream, relativeHandstreamPercent},
                {Skillset.TotalQuadstream, quadStreamPercent},
                {Skillset.RelativeQuadstream, relativeQuadstreamPercent}
            };
        }
    }
}