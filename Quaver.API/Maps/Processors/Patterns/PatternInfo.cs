using System;
using System.Collections.Generic;
using System.Linq;
using Quaver.API.Enums;
using Quaver.API.Helpers;
using Quaver.API.Maps.Structures;

namespace Quaver.API.Maps.Processors.Patterns
{
    public class PatternInfo
    {
        /// <summary>
        ///     The type of pattern this is
        /// </summary>
        public PatternType Type { get; }

        /// <summary>
        ///     The mods used on the play
        /// </summary>
        public ModIdentifier Mods { get; }

        /// <summary>
        ///     The speed of the map based on the activated modifiers
        /// </summary>
        private float Rate => ModHelper.GetRateFromMods(Mods);

        /// <summary>
        ///     The beginning of the pattern
        /// </summary>
        public float StartTime => HitObjects.First().StartTime / Rate;

        /// <summary>
        ///     The ending of the pattern
        /// </summary>
        public float EndTime => HitObjects.Last().StartTime / Rate;

        /// <summary>
        ///     The length of the pattern
        /// </summary>
        public float Length => EndTime - StartTime;

        /// <summary>
        ///     The objects that consist inside the pattern
        /// </summary>
        public List<HitObjectInfo> HitObjects { get; }

        /// <summary>
        ///     The amount of jumps within the pattern
        /// </summary>
        public int JumpChordCount { get; private set; }

        /// <summary>
        ///     The amount of hands within the pattern
        /// </summary>
        public int HandChordCount { get; private set; }

        /// <summary>
        ///     The amount of 4 chords in the pattern
        /// </summary>
        public int QuadChordCount { get; private set; }

        /// <summary>
        ///     The amount of 5+ chords in the pattern
        /// </summary>
        public int FivePlusChordCount { get; private set; }

        /// <summary>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="mods"></param>
        /// <param name="hitObjects"></param>
        public PatternInfo(PatternType type, ModIdentifier mods, List<HitObjectInfo> hitObjects)
        {
            Type = type;
            Mods = mods;
            HitObjects = hitObjects;

            if (HitObjects.Count == 0)
                throw new InvalidOperationException("Cannot create PatternInfo with zero objects");

            DetectChordPatterns();
        }

        /// <summary>
        ///     Prints out information about the pattern
        /// </summary>
        public void Print()
        {
            Console.WriteLine($"Detected Stream Pattern: ");
            Console.WriteLine($"Start Time: {StartTime}");
            Console.WriteLine($"End Time: {EndTime}");
            Console.WriteLine($"Length: " + Length);
            Console.WriteLine($"Objects: " + HitObjects.Count);
            Console.WriteLine($"Jump Count: {JumpChordCount} ({(float) JumpChordCount / HitObjects.Count * 100f:0.00}%)");
            Console.WriteLine($"Hand Count: {HandChordCount} ({(float) HandChordCount / HitObjects.Count * 100f:0.00}%)");
            Console.WriteLine($"Quad Count: {QuadChordCount} ({(float) QuadChordCount / HitObjects.Count * 100f:0.00}%)");
            Console.WriteLine($"5+ Chord Count: {FivePlusChordCount} ({(float) FivePlusChordCount / HitObjects.Count * 100f:0.00}%)");
            Console.WriteLine("--------------");
        }

        /// <summary>
        ///     Finds all of the chords in the pattern and populates
        ///     <see cref="JumpChordCount"/> , <see cref="HandChordCount"/>, <see cref="FivePlusChordCount"/>
        /// </summary>
        private void DetectChordPatterns()
        {
            var grouped = HitObjects.GroupBy(x => x.StartTime / Rate);

            foreach (var group in grouped)
            {
                var count = group.Count();

                switch (count)
                {
                    case 1:
                        break;
                    case 2:
                        JumpChordCount++;
                        break;
                    case 3:
                        HandChordCount++;
                        break;
                    case 4:
                        QuadChordCount++;
                        break;
                }

                if (count >= 5)
                    FivePlusChordCount++;
            }
        }
    }
}