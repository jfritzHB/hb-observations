namespace FieldApp.Domain.FieldItems;

public enum FieldItemType
{
    Observation,
    PunchList,
}

public enum LifecycleState
{
    Draft,
    Published,
}

public enum ItemPriority
{
    Normal,
    High,
    Critical,
}

public enum PhotoStatus
{
    /// <summary>An upload slot was granted; the object may or may not have been uploaded yet.</summary>
    Reserved,

    /// <summary>The server verified the uploaded object, recorded its metadata and created a thumbnail.</summary>
    Finalized,

    /// <summary>Superseded before finalization (for example the user chose a different photo).</summary>
    Abandoned,
}
