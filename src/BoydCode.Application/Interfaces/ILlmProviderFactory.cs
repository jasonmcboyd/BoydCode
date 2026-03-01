using BoydCode.Domain.Configuration;

namespace BoydCode.Application.Interfaces;

public interface ILlmProviderFactory
{
  ILlmProvider Create(LlmProviderConfig config);
}
