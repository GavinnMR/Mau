namespace Mau.Tests.Support;

public sealed class MauWebFactoryWithoutAdmin : MauWebFactory
{
    protected override string? AdminEmail => null;

    protected override string? AdminPassword => null;
}
