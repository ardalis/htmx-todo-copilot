# My Minimal API

This project is a demonstration of a minimal API built with .NET 9. It showcases how to create a simple web application using minimal APIs without the need for traditional controllers.

## Project Structure

- **Program.cs**: Entry point of the application. Sets up the minimal API, configures services, and defines the API endpoints.
- **Models/User.cs**: Contains the `User` model with properties `Id`, `Name`, and `Email`.
- **Services/UserService.cs**: Implements business logic for user data with methods `GetUserById(int id)` and `GetAllUsers()`.
- **Extensions/ServiceExtensions.cs**: Provides a method to register application services in the dependency injection container.
- **appsettings.json**: Configuration settings for the application.
- **appsettings.Development.json**: Development-specific configuration settings.
- **my-minimal-api.csproj**: Project file specifying dependencies and build settings.

## Getting Started

1. Clone the repository:
   ```
   git clone <repository-url>
   ```

2. Navigate to the project directory:
   ```
   cd my-minimal-api
   ```

3. Restore the dependencies:
   ```
   dotnet restore
   ```

4. Run the application:
   ```
   dotnet run
   ```

## API Endpoints

- **GET /users**: Retrieves all users.
- **GET /users/{id}**: Retrieves a user by their ID.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any enhancements or bug fixes.

## License

This project is licensed under the MIT License. See the LICENSE file for more details.