namespace FieldApp.Application.Authorization;

/// <summary>Named project-scoped operations. Each maps to a policy in <see cref="ProjectPolicies"/>.</summary>
public enum ProjectOperation
{
    ViewProject,
    ViewReferenceData,
    ManageAreas,

    /// <summary>Create field items and add their capture photos.</summary>
    CaptureItems,
}
