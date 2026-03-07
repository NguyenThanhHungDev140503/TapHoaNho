using Application.Common.Behaviours;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application;

/// <summary>
/// Extension methods để đăng ký các services của Application layer
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        
        // AutoMapper
        services.AddAutoMapper(assembly);
        
        // FluentValidation - auto-register tất cả validators
        services.AddValidatorsFromAssembly(assembly);
        
        // MediatR - auto-register tất cả handlers
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);
            // Đăng ký ValidationBehaviour pipeline
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        });

        return services;
    }
}
