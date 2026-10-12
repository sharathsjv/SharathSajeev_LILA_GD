using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    public enum NoteKind { Info, Routine, Tip }

    // Something the player has found out. Routine notes with an opening number are highlighted in the notebook:
    // tapping one shows the idea for a kill there.
    [Serializable]
    public class Note
    {
        public string id;
        public string text;
        public NoteKind kind;
        public int day;
        public float minutes;
        public int opening;
    }

    // How well someone remembers the player, per look. Changing the top (or the hair) halves it; changing both
    // and they don't recognise you at all. Changing back brings it all back.
    [Serializable]
    public class InfamyEntry
    {
        public string person, top, hair;
        public float value;
    }

    [Serializable]
    public class Var
    {
        public string key;
        public float value;
    }

    // Everything about the player's progress that a checkpoint has to bring back: cash, pockets, what's in the
    // wardrobe, things they've done in the world (flags / numbers), notes, and who remembers them.
    [Serializable]
    public class State
    {
        public const int PocketSize = 5;

        public int cash = 20;
        public List<string> items = new List<string>();
        public List<string> stash = new List<string>();
        [Tooltip("Clothes the player owns, as 'slot|from|colour' (from = character index, -1 = their own).")]
        public List<string> clothes = new List<string>();
        [Tooltip("Items picked up in a shop and not paid for yet: 'shopId|item'.")]
        public List<string> unpaid = new List<string>();
        public List<string> flags = new List<string>();
        public List<Var> vars = new List<Var>();
        public List<Note> notes = new List<Note>();
        public string notepad = "";
        public List<InfamyEntry> infamy = new List<InfamyEntry>();

        public State Copy() => JsonUtility.FromJson<State>(JsonUtility.ToJson(this));

        // ---- flags & numbers ----
        public bool Has(string flag) => flags.Contains(flag);
        public void Set(string flag, bool on = true)
        {
            if (on && !flags.Contains(flag)) flags.Add(flag);
            else if (!on) flags.Remove(flag);
        }

        public float Get(string key, float fallback = 0)
        {
            foreach (var v in vars) if (v.key == key) return v.value;
            return fallback;
        }

        public void Put(string key, float value)
        {
            foreach (var v in vars) if (v.key == key) { v.value = value; return; }
            vars.Add(new Var { key = key, value = value });
        }

        // ---- clothes ----
        public static string Garment(Slot slot, int from, int colour) => $"{slot}|{from}|{colour}";
        public bool Owns(Slot slot, int from, int colour) => clothes.Contains(Garment(slot, from, colour));
        public void AddClothes(Slot slot, int from, int colour)
        {
            var key = Garment(slot, from, colour);
            if (!clothes.Contains(key)) clothes.Add(key);
        }

        // ---- pockets ----
        public bool Full => items.Count >= PocketSize;
        public bool Holding(string item) => items.Contains(item);
        public int Count(string item) { int n = 0; foreach (var i in items) if (i == item) n++; return n; }

        // ---- infamy ----
        public float InfamyOf(string person, string top, string hair) => Recognise(person, top, hair, out _, out _);

        // How well this person knows the player as they look now, and which part of the look they'd recognise:
        // the same top and hair = everything they remember; only one of them the same = half; neither = a stranger.
        public float Recognise(string person, string top, string hair, out bool sameTop, out bool sameHair)
        {
            float best = 0;
            sameTop = sameHair = false;
            foreach (var e in infamy)
            {
                if (e.person != person) continue;
                bool t = e.top == top, h = e.hair == hair;
                float v = t && h ? e.value : t || h ? e.value * 0.5f : 0;
                if (v > best) { best = v; sameTop = t; sameHair = h; }
            }
            return best;
        }

        // The person who'd recognise the player best right now (null if nobody would).
        public float MostKnown(string top, string hair, out bool sameTop, out bool sameHair, out string person)
        {
            float best = 0;
            sameTop = sameHair = false;
            person = null;
            foreach (var e in infamy)
            {
                float v = Recognise(e.person, top, hair, out var t, out var h);
                if (v > best) { best = v; sameTop = t; sameHair = h; person = e.person; }
            }
            return best;
        }

        public float RaiseInfamy(string person, string top, string hair)
        {
            float now = InfamyOf(person, top, hair) + 1;
            foreach (var e in infamy)
                if (e.person == person && e.top == top && e.hair == hair) { e.value = now; return now; }
            infamy.Add(new InfamyEntry { person = person, top = top, hair = hair, value = now });
            return now;
        }
    }

    // A saved moment to go back to when the player is caught: made every time they write something new in the notebook.
    public class Checkpoint
    {
        public int day;
        public float minutes, hunger, tiredness;
        public string area;
        public Vector3 position;
        public Quaternion rotation;
        public State state;
        public Outfit outfit;
        public bool[] alive;
    }
}
