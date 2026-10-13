using System;
using System.IO;
using AICompanion.Services;
using UnityEngine;

// Batch-mode smoke checks for the optional-content boundary.
public static class PortfolioVerification
{
    public static void VerifyCorePoses()
    {
        if (Directory.Exists(Path.Combine(Application.dataPath, "OptionalContent")))
            throw new InvalidOperationException("Core check requires no optional content.");
        Verify(false);
    }

    public static void VerifyInstalledPoses()
    {
        if (!Directory.Exists(Path.Combine(Application.dataPath, "OptionalContent")))
            throw new InvalidOperationException("Installed check requires optional content.");
        Verify(true);
    }

    private static void Verify(bool installed)
    {
        var target = new GameObject("Portfolio pose verification");
        try
        {
            var service = target.AddComponent<VRM10Service>();
            var poses = service.GetAvailablePoses();
            if (poses.Count == 0)
                throw new InvalidOperationException("Core pose catalog must not be empty.");
            if (installed && !poses.Contains("Optional Pose"))
                throw new InvalidOperationException("Installed catalog was not discovered.");
            Debug.Log($"PORTFOLIO_POSE_CHECK_OK installed={installed} count={poses.Count}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
