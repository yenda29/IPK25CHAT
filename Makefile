TARGET=ipk25chat-client
RUNTIME=linux-x64
OUTPUT_DIR=.
SRC_DIR=src

all: build

build:
	dotnet publish $(SRC_DIR) -c Release -r $(RUNTIME) \
		/p:PublishSingleFile=true \
		/p:PublishTrimmed=true \
		/p:AssemblyName=$(TARGET) \
		-o $(OUTPUT_DIR)
		

clean:
	dotnet clean $(SRC_DIR)
	rm -f $(TARGET)