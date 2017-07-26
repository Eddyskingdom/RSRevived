﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zaimoni.Data
{
    // Grammar support.  Note that English has very simple agreement rules compared to most natural languages.
    public static class LangExt
    {
        public enum Gender
        {
          MALE = 0,
          FEMALE,
          NEUTER    // last of basic three genders
        }

        public enum Plurality
        {
          SINGULAR = 0,
          PLURAL    // last of basic plurality
        }

        // most languages react to this for agreement purposes
        public static bool StartsWithVowel(this string name)
        {
            return 0 <= "AEIOUaeiou".IndexOf(name[0]);
        }

        // names of functions are English-centric
        public static string PrefixIndefiniteSingularArticle(this string name)
        {
          return (name.StartsWithVowel() ? "an " : "a ")+name;
        }

        public static string PrefixIndefinitePluralArticle(this string name)
        {
          return "some "+name;
        }
        public static string PrefixDefiniteSingularArticle(this string name)
        {
            return "the "+name;
        }

        public static string PrefixDefinitePluralArticle(this string name)
        {
            return "some "+name;
        }

        // XXX incomplete implementation; have a grammar text available but past a certain point you need a Noun or Verb class.
        public static string Plural(this string name, bool plural)
        {
          if (!plural) return name;
          return name+"s";
        }

        public static string Plural(this string name, int qty)
        {
            if (1 == qty) return name;
            return name + "s";
        }

        // numeric.  The verbal version would be FormalQtyDesc or QtyDescFormal
        public static string QtyDesc(this string name, int qty)
        {
            return qty.ToString() + " " + name.Plural(qty);
        }
    }
}