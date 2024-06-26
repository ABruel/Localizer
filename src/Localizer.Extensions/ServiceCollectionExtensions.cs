﻿using System;
using Localizer.Extensions.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Localizer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddI18NextLocalization(this IServiceCollection services)
    {
        return AddI18NextLocalization(services, null);
    }

    public static IServiceCollection AddI18NextLocalization(this IServiceCollection services, Action<I18NextBuilder> i18Next)
    {
        var builder = new I18NextBuilder(services);

        i18Next?.Invoke(builder);

        builder.Build();

        return services;
    }
}