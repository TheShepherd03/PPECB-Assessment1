using System.ComponentModel.DataAnnotations;

namespace PPECB.Application.DTOs;

// ---------- Excel import ----------

/// <summary>One parsed spreadsheet row, before business validation.</summary>
public class ExcelProductRow
{
    public int RowNumber { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? CategoryCode { get; set; }
    public string? CategoryName { get; set; }
    public string? PriceRaw { get; set; }
    public decimal? Price { get; set; }
}

/// <summary>Outcome of reading the workbook: the rows plus any structural problems.</summary>
public class ExcelImportParseResult
{
    public List<ExcelProductRow> Rows { get; set; } = new();
    public List<ExcelImportErrorDto> Errors { get; set; } = new();
}

public class ExcelImportErrorDto
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;

    public ExcelImportErrorDto() { }

    public ExcelImportErrorDto(int rowNumber, string message)
    {
        RowNumber = rowNumber;
        Message = message;
    }
}

/// <summary>
/// Import summary returned to the client. The import is all-or-nothing: when any row
/// fails, nothing is saved and every problem is listed so the user can fix the file once.
/// </summary>
public class ExcelImportResultDto
{
    public bool Succeeded { get; set; }
    public int RowsRead { get; set; }
    public int ProductsImported { get; set; }
    public List<ExcelImportErrorDto> Errors { get; set; } = new();
}

// ---------- Authentication ----------

public class RegisterRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
