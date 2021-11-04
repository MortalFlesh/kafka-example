Kafka Example
=============

> Example for kafka with F# in docker

## Configuration

```env
KAFKA_BROKER_LIST: "..."
KAFKA_TOPIC: "..."
```

---

## Development
### Requirements
- [dotnet core](https://dotnet.microsoft.com/learn/dotnet/hello-world-tutorial)
- [FAKE](https://fake.build/fake-gettingstarted.html)

### Build
```bash
./build.sh
```

### Run
```bash
./build.sh -t Run
```

### Watch
```bash
./build.sh -t Watch
```
