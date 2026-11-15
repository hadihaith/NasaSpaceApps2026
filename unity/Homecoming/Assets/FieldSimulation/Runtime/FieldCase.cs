using System;
using System.IO;
using UnityEngine;

namespace Homecoming.Field
{
    enum InvestigationKind { Free, Rainfall, Soil, Temperature }
    enum Direction { Unset, Rise, Fall, LittleChange }

    [Serializable] sealed class FiledFinding
    {
        public string id, datasetId, rawSha256, fingerprint, mode, reviewer, reviewRationale, variable, place, footprint, product, version, provider, url, unit, aggregation;
        public string window, significance, status, test, hypothesis, review, reason;
        public int count;
        public bool hasRate, hasPValue, hasInterval, hasOlsRate;
        public float ratePerDecade, fittedChange, pValue, alpha, intervalLow, intervalHigh, olsRatePerDecade;
    }

    [Serializable] sealed class InvestigationRun
    {
        public string id, runId, dataFingerprint, parentId, branchId, regionId, modelVersion, startMonth, endMonth, metric, unit;
        public Forcing inherited, edited;
        public float reference, scenario, delta, tolerance, startingStorage, endingStorage;
        public Direction result;
    }

    [Serializable] sealed class Investigation
    {
        public string id = Guid.NewGuid().ToString("N");
        public InvestigationKind kind;
        public FiledFinding origin, other, difference;
        public Direction originCall, otherCall, scenarioCall, revisedOriginCall, revisedOtherCall;
        public bool callsRevealed;
        public InvestigationRun first, second;
        public string comparisonId, relationship, relationshipScope, gap, conclusion, campaignId, submittedUtc, committingRunId;
        public string soilInterpretation, soilInterpretationRevisionId;
        public bool HasSoilInterpretation => second != null && second.id == soilInterpretationRevisionId &&
            !string.IsNullOrWhiteSpace(soilInterpretation);
        public bool IsReady => origin != null && origin.hasRate &&
            (kind == InvestigationKind.Temperature ? originCall != Direction.Unset && origin.hasPValue :
                !string.IsNullOrEmpty(relationship) && (other != null || !string.IsNullOrWhiteSpace(gap))) &&
            (kind != InvestigationKind.Rainfall || (originCall != Direction.Unset && otherCall != Direction.Unset &&
                difference?.hasPValue == true)) &&
            (kind != InvestigationKind.Soil || (scenarioCall != Direction.Unset && first != null && second != null &&
                (HasSoilInterpretation || !string.IsNullOrEmpty(submittedUtc)) &&
                (first.result == scenarioCall || !string.IsNullOrWhiteSpace(conclusion))));
    }

    [Serializable] sealed class FieldReport
    {
        public int schemaVersion = 1;
        public string submittedUtc, recruit, question;
        public Investigation caseFile;
    }

    static class FieldCaseStore
    {
        const string Key = "homecoming.unity.investigation.v1";
        public const string FolderName = "Investigation reports";
        public static string Folder => Path.Combine(Application.persistentDataPath, FolderName);

        public static Investigation Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            try
            {
                var active = (InvestigationKind)PlayerPrefs.GetInt(Key);
                return Find(active);
            }
            catch (ArgumentException) { return null; }
        }

        public static Investigation Find(InvestigationKind kind)
        {
            Investigation result;
            try { result = JsonUtility.FromJson<Investigation>(PlayerPrefs.GetString(Key+"."+kind, "")); }
            catch (ArgumentException) { return null; }
            if (result == null || string.IsNullOrWhiteSpace(result.id) || result.kind != kind) return null;
            if (string.IsNullOrEmpty(result.origin?.id)) result.origin = null;
            if (string.IsNullOrEmpty(result.other?.id)) result.other = null;
            if (string.IsNullOrEmpty(result.difference?.id)) result.difference = null;
            if (string.IsNullOrEmpty(result.first?.id)) result.first = null;
            if (string.IsNullOrEmpty(result.second?.id)) result.second = null;
            // JsonUtility serializes unset strings as empty strings. Restore the unset comparison.
            if (string.IsNullOrWhiteSpace(result.relationship)) result.relationship = null;
            if (string.IsNullOrWhiteSpace(result.gap)) result.gap = null;
            return result;
        }

        public static void Save(Investigation value)
        {
            PlayerPrefs.SetInt(Key, (int)value.kind);
            PlayerPrefs.SetString(Key+"."+value.kind, JsonUtility.ToJson(value));
            PlayerPrefs.Save();
        }

        internal static void ClearSavedCases()
        {
            PlayerPrefs.DeleteKey(Key);
            foreach (InvestigationKind kind in Enum.GetValues(typeof(InvestigationKind)))
                PlayerPrefs.DeleteKey(Key+"."+kind);
        }

        public static string Export(FieldReport report, string outputFolder = null)
        {
            string folder = outputFolder ?? Folder;
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, report.caseFile.id + ".json");
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(report, true));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            return path;
        }
    }
}
