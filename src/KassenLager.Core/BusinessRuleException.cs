namespace KassenLager.Core;

/// <summary>
/// A business rule was violated. The message is meant for the user (German) and is
/// shown as-is; technical failures use other exception types and are logged.
/// </summary>
public class BusinessRuleException(string message) : Exception(message);

public sealed class EntityNotFoundException() : BusinessRuleException(Messages.NotFound);
