namespace BoydCode.Domain.Enums;

public enum ExitCode
{
  Success = 0,
  GeneralError = 1,
  AuthenticationError = 2,
  ProviderError = 3,
  ConfigurationError = 4,
  NetworkError = 5,
  UserCancelled = 130,
}
