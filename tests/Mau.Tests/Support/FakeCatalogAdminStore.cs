using Mau.Core.Admin;

namespace Mau.Tests.Support;

public sealed class FakeCatalogAdminStore : ICatalogAdminStore
{
    public List<Call> Calls { get; } = new();

    public int IngredientIdToReturn { get; set; } = 101;

    public int DishIdToReturn { get; set; } = 101;

    public Exception? ExceptionToThrow { get; set; }

    public Task<int> AddIngredientAsync(
        IngredientInput input, CancellationToken ct = default)
    {
        Record(new Call(nameof(AddIngredientAsync), null, input, null));
        return Task.FromResult(IngredientIdToReturn);
    }

    public Task UpdateIngredientAsync(
        int id, IngredientInput input, CancellationToken ct = default)
    {
        Record(new Call(nameof(UpdateIngredientAsync), id, input, null));
        return Task.CompletedTask;
    }

    public Task DeleteIngredientAsync(int id, CancellationToken ct = default)
    {
        Record(new Call(nameof(DeleteIngredientAsync), id, null, null));
        return Task.CompletedTask;
    }

    public Task<int> AddDishAsync(DishInput input, CancellationToken ct = default)
    {
        Record(new Call(nameof(AddDishAsync), null, null, input));
        return Task.FromResult(DishIdToReturn);
    }

    public Task UpdateDishAsync(int id, DishInput input, CancellationToken ct = default)
    {
        Record(new Call(nameof(UpdateDishAsync), id, null, input));
        return Task.CompletedTask;
    }

    public Task DeleteDishAsync(int id, CancellationToken ct = default)
    {
        Record(new Call(nameof(DeleteDishAsync), id, null, null));
        return Task.CompletedTask;
    }

    private void Record(Call call)
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        Calls.Add(call);
    }

    public sealed record Call(
        string Method, int? Id, IngredientInput? IngredientInput, DishInput? DishInput);
}
