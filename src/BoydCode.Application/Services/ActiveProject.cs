namespace BoydCode.Application.Services;

public sealed class ActiveProject
{
  public string? Name { get; private set; }

  public void Set(string name)
  {
    Name = name;
  }
}
