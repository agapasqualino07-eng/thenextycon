using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using OfficinaTycoon.UnityLayer;

namespace OfficinaTycoon.Tests
{
    // Tests for the UI texts and number formatting shared by the Unity and web front-ends.
    // Kept outside Assets/Tests because those files are pure C# but live in the Unity assembly (not referenced by the EditMode tests).
    public class TextAndFormatTests
    {
        [TestCase(0, "0")]
        [TestCase(0.5, "0.5")]
        [TestCase(1.01, "1.01")]
        [TestCase(9.999, "9.99")]
        [TestCase(999.9, "999")]
        [TestCase(1000, "1.00K")]
        [TestCase(1500, "1.50K")]
        [TestCase(99_999, "99.9K")]
        [TestCase(999_999, "999K")]
        [TestCase(999_999_999, "999M")]
        [TestCase(1_000_000_000, "1.00B")]
        [TestCase(30_000_000_000, "30.0B")]
        [TestCase(1e24, "1.00Sp")]
        public void Num_FormatsWithSuffixes_AndNeverRoundsUp(double v, string expected)
        {
            Assert.AreEqual(expected, Fmt.Num(v));
        }

        [Test]
        public void Num_HandlesNonFiniteAndNegative()
        {
            Assert.AreEqual("0", Fmt.Num(double.NaN));
            Assert.AreEqual("0", Fmt.Num(double.PositiveInfinity));
            Assert.AreEqual("-1.50K", Fmt.Num(-1500));
        }

        [TestCase(0, "00:00")]
        [TestCase(59.2, "01:00")]
        [TestCase(90, "01:30")]
        [TestCase(3600, "1h 00m")]
        [TestCase(8 * 3600 + 5 * 60, "8h 05m")]
        [TestCase(-5, "00:00")]
        public void Time_Formats(double s, string expected) { Assert.AreEqual(expected, Fmt.Time(s)); }

        [Test]
        public void Loc_EveryTextFormatsWithArguments_AndHasNoAsciiAccents()
        {
            var field = typeof(Loc).GetField("Tables", BindingFlags.NonPublic | BindingFlags.Static);
            var tables = (Dictionary<string, Dictionary<string, string>>)field.GetValue(null);
            var it = tables["it"];
            Assert.Greater(it.Count, 100);
            foreach (var kv in it)
            {
                Assert.DoesNotThrow(() => string.Format(kv.Value, "a", "b", "c", "d", "e"), kv.Key);
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(kv.Value, @"\b[A-Za-z]+[aeiou]'(\s|$|[.,!?""])"),
                    kv.Key + ": use real accented letters, not apostrophes: " + kv.Value);
            }
        }

        [Test]
        public void Loc_MissingKeyFallsBackToKey()
        {
            Assert.AreEqual("no.such.key", Loc.T("no.such.key"));
            Assert.AreEqual("Officina Tycoon", Loc.T("app.title"));
        }

        [Test]
        public void Loc_AllKeysUsedByTheCodeExist()
        {
            // Scan the front-end sources for Loc.T("...") keys and verify each exists in the Italian table.
            string root = TestContext.CurrentContext.TestDirectory;
            while (root != null && !System.IO.Directory.Exists(System.IO.Path.Combine(root, "Assets"))) root = System.IO.Path.GetDirectoryName(root);
            Assert.IsNotNull(root, "repository root not found");
            var files = System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "Assets", "Scripts", "Unity"), "*.cs")
                .Concat(System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "Web", "OfficinaTycoon.Web"), "*.*", System.IO.SearchOption.AllDirectories)
                    .Where(f => (f.EndsWith(".razor") || f.EndsWith(".cs")) && !f.Contains("/obj/") && !f.Contains("/bin/")));
            var keys = new HashSet<string>();
            foreach (var f in files)
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(f), "Loc\\.T\\(\"([a-z0-9_.]+)\""))
                    keys.Add(m.Groups[1].Value);
            Assert.Greater(keys.Count, 80);
            var missing = keys.Where(k => Loc.T(k) == k).ToList();
            Assert.IsEmpty(missing, "missing texts: " + string.Join(", ", missing));
        }
    }
}
