﻿using System.Collections;
using System.Collections.Generic;
using System.Json;
using System.Linq;

namespace chess_pos_db_gui
{
    public class SegregatedEntries : IEnumerable<KeyValuePair<Origin, Entry>>
    {
        private Dictionary<Origin, Entry> Entries { get; set; }

        public static SegregatedEntries FromJson(JsonValue json)
        {
            var e = new SegregatedEntries();

            foreach (KeyValuePair<string, JsonValue> byLevel in json)
            {
                GameLevel level = GameLevelHelper.FromString(byLevel.Key).First();
                foreach (KeyValuePair<string, JsonValue> byResult in byLevel.Value)
                {
                    GameResult result = GameResultHelper.FromStringWordFormat(byResult.Key).First();

                    e.Add(level, result, Entry.FromJson(byResult.Value));
                }
            }

            return e;
        }

        public SegregatedEntries()
        {
            Entries = new Dictionary<Origin, Entry>();
        }

        public void Add(GameLevel level, GameResult result, Entry entry)
        {
            Entries.Add(new Origin(level, result), entry);
        }

        public Entry Get(GameLevel level, GameResult result)
        {
            if (Entries.TryGetValue(new Origin(level, result), out Entry e))
            {
                return e;
            }

            return null;
        }

        IEnumerator<KeyValuePair<Origin, Entry>> IEnumerable<KeyValuePair<Origin, Entry>>.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<Origin, Entry>>)Entries).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<Origin, Entry>>)Entries).GetEnumerator();
        }
    }

    internal struct Origin
    {
        public GameLevel Level { get; set; }
        public GameResult Result { get; set; }

        public Origin(GameLevel level, GameResult result)
        {
            Level = level;
            Result = result;
        }
    }
}
