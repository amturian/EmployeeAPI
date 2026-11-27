using FluentValidation;

public class UpdateEmployeeRequest
{
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
}

public class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    private readonly HttpContext _httpContext;
    private readonly AppDbContext appDbContext;

    public UpdateEmployeeRequestValidator(IHttpContextAccessor httpContextAccessor, AppDbContext appDbContext)
    {
        this._httpContext = httpContextAccessor.HttpContext!;

        RuleFor(x => x.Address1)
            .MustAsync(NotBeEmptyIfItIsSetOnEmployeeAlreadyAsync)
            .WithMessage("Address1 must not be empty.");
        this.appDbContext = appDbContext;
    }

    private async Task<bool> NotBeEmptyIfItIsSetOnEmployeeAlreadyAsync(string? address, CancellationToken token)
    {
        await Task.CompletedTask;   //again, we'll not make this async for now!

        var id = Convert.ToInt32(_httpContext.Request.RouteValues["id"]);
        var employee = await appDbContext.Employees.FindAsync(id);

        if (employee != null && employee!.Address1 != null && string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        return true;
    }
}