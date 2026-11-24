namespace LisbergThreads.Api.Services;

public sealed class ProductUploadPath
{
    public string Value { get; }

    public ProductUploadPath(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var path = configuration["Uploads:ProductsPath"];
        if (string.IsNullOrWhiteSpace(path) && environment.IsDevelopment())
        {
            path = OperatingSystem.IsWindows()
                ? configuration.GetValue<bool>("Uploads:UseRemotePath")
                    ? configuration["Uploads:RemoteProductsPath"]
                    : configuration["Uploads:WindowsProductsPath"]
                : Environment.MachineName.Equals("nuggit-laptop", StringComparison.OrdinalIgnoreCase)
                    ? configuration["Uploads:LinuxLaptopProductsPath"]
                    : configuration["Uploads:LinuxProductsPath"];
        }
        Value = Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? "App_Data/uploads/products" : path,
            environment.ContentRootPath);
        Directory.CreateDirectory(Value);
    }
}
