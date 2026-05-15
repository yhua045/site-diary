namespace SiteDiary.Application.Shared;

public enum OperationStatus { Success, NotFound, Forbidden }

public record OperationResult<T>(OperationStatus Status, T? Value = default)
{
    public static OperationResult<T> Ok(T value) => new(OperationStatus.Success, value);
    public static OperationResult<T> NotFound() => new(OperationStatus.NotFound);
    public static OperationResult<T> Forbidden() => new(OperationStatus.Forbidden);
}
