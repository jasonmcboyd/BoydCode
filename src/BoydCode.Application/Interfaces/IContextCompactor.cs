using BoydCode.Domain.Entities;

namespace BoydCode.Application.Interfaces;

public interface IContextCompactor
{
  Task<Conversation> CompactAsync(Conversation conversation, int targetTokenCount, CancellationToken ct = default);
}
