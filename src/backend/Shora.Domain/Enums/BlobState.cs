namespace Shora.Domain.Enums;

public enum BlobState
{
    TempUploaded = 0,
    Finalized = 1,
    BlobFinalizePending = 2,
    Missing = 3
}
