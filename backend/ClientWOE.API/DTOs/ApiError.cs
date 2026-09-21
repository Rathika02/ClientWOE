namespace ClientWOE.API.DTOs;

/// Consistent error body for 404 / 409 / 500 responses.
public record ApiError(int Status, string Message);
