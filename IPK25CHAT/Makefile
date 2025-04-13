TARGET=ipk25chat-client

all: build clean

# Build the project
build:
	dotnet publish -c Release -r linux-x64 /p:PublishSingleFile=true /p:PublishTrimmed=true -o ./

# Clean up the build
clean:
	dotnet clean