using BoydCode.Domain.Entities;

namespace BoydCode.Application.Services;

public sealed class ActiveSession
{
  public Session? Session { get; private set; }

  public void Set(Session session)
  {
    Session = session;
  }
}
