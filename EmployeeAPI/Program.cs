using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Internal;

var builder = WebApplication.CreateBuilder(args);
var employees = new List<Employee>
{
    // new Employee { Id = 1, FirstName = "John", LastName = "Doe", SocialSecurityNumber ="22",   Benefits = new List<EmployeeBenefits>
    //     {
    //         new EmployeeBenefits { BenefitType = BenefitType.Health, Cost = 100 },
    //         new EmployeeBenefits { BenefitType = BenefitType.Dental, Cost = 50 }
    //     } },
    // new Employee { Id = 2, FirstName = "Jane", LastName = "Doe", SocialSecurityNumber = "0000" ,   Benefits = new List<EmployeeBenefits>
    //     {
    //         new EmployeeBenefits { BenefitType = BenefitType.Health, Cost = 100 },
    //         new EmployeeBenefits { BenefitType = BenefitType.Dental, Cost = 50 }
    //     }}

};

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "EmployeeApi.xml"));
});
builder.Services.AddProblemDetails();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<FluentValidationFilter>();
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ISystemClock, SystemClock>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    SeedData.MigrateAndSeed(services);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.UseHttpsRedirection();

app.Run();

public partial class Program { }
