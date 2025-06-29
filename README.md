# Habit Tracker Backend API

A comprehensive ASP.NET Core 8.0 Web API for habit tracking with social features, AI-powered suggestions, and real-time notifications.

## 🚀 Features

- **User Authentication** - JWT-based authentication with refresh tokens
- **Habit Management** - Create, track, and manage personal habits
- **Social Features** - Connect with friends and view their public habits
- **Cheering System** - Encourage friends with emoji-based cheers
- **AI Suggestions** - OpenAI-powered habit recommendations
- **Notifications** - Real-time updates for social interactions
- **Progress Tracking** - Detailed analytics and streak tracking

## 🛠️ Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/download/)
- [OpenAI API Key](https://platform.openai.com/) (optional, for AI features)

## ⚙️ Environment Setup

### 1. Configure Environment Variables

Create the following environment variables for secure configuration:
# Required - Database
POSTGRESQL_CONNECTION_STRING=Host=localhost;Database=habittracker;Username=your_username;Password=your_password

# Required - JWT Authentication
JWT_SECRET=your-super-secret-jwt-key-at-least-32-characters-long
JWT_ISSUER=https://localhost:5001

# Optional - AI Features
OPENAI_API_KEY=sk-your-openai-api-key-here

# Optional - Google OAuth
GOOGLE_CLIENT_ID=your-google-client-id
GOOGLE_CLIENT_SECRET=your-google-client-secret
### 2. Windows Environment Variables
# Set environment variables (Windows)
setx POSTGRESQL_CONNECTION_STRING "Host=localhost;Database=habittracker;Username=your_username;Password=your_password"
setx JWT_SECRET "your-super-secret-jwt-key-at-least-32-characters-long"
setx JWT_ISSUER "https://localhost:5001"
setx OPENAI_API_KEY "sk-your-openai-api-key-here"
### 3. Linux/macOS Environment Variables
# Add to ~/.bashrc or ~/.zshrc
export POSTGRESQL_CONNECTION_STRING="Host=localhost;Database=habittracker;Username=your_username;Password=your_password"
export JWT_SECRET="your-super-secret-jwt-key-at-least-32-characters-long"
export JWT_ISSUER="https://localhost:5001"
export OPENAI_API_KEY="sk-your-openai-api-key-here"
## 🗄️ Database Setup

### 1. Create PostgreSQL Database
CREATE DATABASE habittracker;
### 2. Run Migrations
dotnet ef database update
## 🏃‍♂️ Running the Application

### 1. Restore Dependencies
dotnet restore
### 2. Build the Project
dotnet build
### 3. Run the Application
dotnet run
The API will be available at: `https://localhost:5001` or `http://localhost:5000`

## 📚 API Documentation

Once running, access the Swagger UI at: `https://localhost:5001/swagger`

### Authentication

All protected endpoints require a JWT token in the Authorization header:
Authorization: Bearer <your-jwt-token>
### Key Endpoints

- **Authentication**: `/api/user/*`
- **Habits**: `/api/habits/*`
- **Social**: `/api/connection/*`
- **Cheering**: `/api/cheer/*`
- **AI Suggestions**: `/api/ai/*`
- **Notifications**: `/api/notification/*`

## 🔧 Development Tools

### User Secrets (Development Only)

For development, you can use .NET User Secrets instead of environment variables:
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=habittracker;Username=dev;Password=dev123"
dotnet user-secrets set "Jwt:Key" "your-development-jwt-secret-key"
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-openai-key"
### Test Endpoints

Use the provided `API_TESTS.http` file with your HTTP client to test all endpoints.

## 🚀 Production Deployment

### 1. Environment Configuration

Set production environment variables on your hosting platform:

- `POSTGRESQL_CONNECTION_STRING` - Production database connection
- `JWT_SECRET` - Strong JWT secret key
- `JWT_ISSUER` - Your production domain
- `OPENAI_API_KEY` - OpenAI API key (if using AI features)

### 2. CORS Configuration

Update CORS settings in `Program.cs` for your frontend domain:
app.UseCors(policy =>
    policy.WithOrigins("https://yourapp.com")
          .AllowAnyMethod()
          .AllowAnyHeader());
### 3. Build for Production
dotnet publish -c Release -o ./publish
## 🔒 Security Features

- **JWT Authentication** with refresh token rotation
- **BCrypt Password Hashing**
- **Environment Variable Configuration** (no secrets in code)
- **Input Validation** and sanitization
- **CORS Protection**
- **Authorization** on all protected endpoints

## 🎯 Features Status

| Feature | Status | Description |
|---------|--------|-------------|
| Authentication | ✅ Complete | JWT-based auth with refresh tokens |
| User Management | ✅ Complete | Profile management and user operations |
| Habit CRUD | ✅ Complete | Full habit lifecycle management |
| Progress Tracking | ✅ Complete | Streaks, logs, and analytics |
| Social Connections | ✅ Complete | Friend requests and connections |
| Habit Cheering | ✅ Complete | Emoji-based encouragement system |
| Notifications | ✅ Complete | Real-time updates and alerts |
| AI Suggestions | ✅ Complete | OpenAI-powered habit recommendations |
| Public Habits | ✅ Complete | Discover and follow public habits |

## 🛡️ Troubleshooting

### Common Issues

1. **Database Connection Errors**
   - Verify PostgreSQL is running
   - Check connection string format
   - Ensure database exists

2. **JWT Token Issues**
   - Verify JWT_SECRET is at least 32 characters
   - Check token expiration
   - Ensure proper Authorization header format

3. **AI Features Not Working**
   - Verify OPENAI_API_KEY is set
   - Check OpenAI account credits
   - Review API logs for errors

### Push Protection Issues

If you encounter GitHub push protection errors:

1. Ensure no actual secrets are in configuration files
2. Use environment variables for all sensitive data
3. Check `.gitignore` includes `.env` files
4. Remove any committed secrets from git history

## 📄 License

This project is licensed under the MIT License.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 📞 Support

For support and questions, please open an issue on GitHub.
