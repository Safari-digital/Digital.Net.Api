using Microsoft.Extensions.DependencyInjection;

namespace Digital.Net.Lib.Validation;

public static class ContractValidation
{
    /// <summary>
    ///     Declares <typeparamref name="TContract" /> as a required contract, i.e. a service
    ///     that must be supplied by a provider. This only records the requirement as a
    ///     <see cref="RequiredContract" /> marker; it does not register an implementation itself.
    /// </summary>
    /// <typeparam name="TContract">The contract type that must be provided.</typeparam>
    /// <param name="services">The service collection to record the requirement in.</param>
    /// <param name="providerHint">
    ///     A hint identifying the provider expected to fulfil the contract
    ///     (e.g. a module or plugin name), used when resolving missing contracts.
    /// </param>
    public static IServiceCollection RequireContract<TContract>(this IServiceCollection services, string providerHint)
        where TContract : class
        => services.AddSingleton(new RequiredContract(typeof(TContract), providerHint));

    /// <summary>
    ///     Validates that every contract declared via
    ///     <see cref="RequireContract{TContract}(IServiceCollection, string)" /> has a registered implementation.
    ///     Intended to be called once at startup, right after the container is built.
    /// </summary>
    /// <param name="services">The built service provider to inspect.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when one or more required contracts have no registered implementation.
    /// </exception>
    public static void ValidateRequiredContracts(this IServiceProvider services)
    {
        var inspector = services.GetRequiredService<IServiceProviderIsService>();
        var missing = services
            .GetServices<RequiredContract>()
            .Where(contract => !inspector.IsService(contract.ContractType))
            .ToList();

        if (missing.Count == 0)
            return;

        var details = string.Join(
            '\n',
            missing.Select(m => $"  - {m.ContractType.Name} (register via {m.ProviderHint})")
        );

        throw new InvalidOperationException(
            $"Digital.Net startup: missing required contract implementation(s):\n{details}"
        );
    }
}