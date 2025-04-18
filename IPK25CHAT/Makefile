TARGET=ipk25chat-client
RUNTIME=linux-x64

all: build

build:
	dotnet publish -c Release -r $(RUNTIME) \
		/p:PublishSingleFile=true \
		/p:PublishTrimmed=true \
		/p:AssemblyName=$(TARGET) \
		-o ./

clean:
	dotnet clean
	rm -f $(TARGET)