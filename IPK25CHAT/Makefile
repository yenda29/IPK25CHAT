TARGET=ipk25chat-client
RUNTIME=linux-x64
OUTPUT_DIR=.

all: build

build:
	dotnet publish -c Release -r $(RUNTIME) \
		/p:PublishSingleFile=true \
		/p:PublishTrimmed=true \
		/p:AssemblyName=$(TARGET) \
		-o $(OUTPUT_DIR)
		

clean:
	dotnet clean
	rm -f $(TARGET)