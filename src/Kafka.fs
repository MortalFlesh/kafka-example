namespace MF.Kafka

open System
open Confluent.Kafka

type BrokerList = BrokerList of string
type Topic = Topic of string

[<RequireQualifiedAccess>]
module Producer =
    type KafkaProducer =
        | KafkaProducer of IProducer<Null, string>

        member this.Close() =
            let (KafkaProducer producer) = this
            printfn "    [KafkaProducer] - flushing ..."
            producer.Flush()
            printfn "    [KafkaProducer] - disposing ..."
            producer.Dispose()
            printfn "    [KafkaProducer] - disposed"

        interface IDisposable with
            member this.Dispose() =
                this.Close()

    type private KafkaMessage = Message<Null, string>

    let connect (BrokerList brokerList) =
        let config =
            ProducerConfig(
                BootstrapServers = brokerList,
                Debug = "broker,topic,msg"
            )

        printfn "    [KafkaProducer] connecting ..."
        let producer = ProducerBuilder(config).Build()
        printfn "    [KafkaProducer] connected"
        KafkaProducer producer

    let private createKafkaMessage message =
        KafkaMessage(
            Value = message
        )

    let produce (KafkaProducer producer) (Topic topic) message =
        printfn "    [KafkaProducer] - producing message ..."
        producer.Produce(topic, createKafkaMessage message)
        printfn "    [KafkaProducer] - message produced"

[<RequireQualifiedAccess>]
module Consumer =
    type KafkaConsumer =
        | KafkaConsumer of IConsumer<Ignore, string>
            member this.Close() =
                let (KafkaConsumer consumer) = this
                printfn "    [KafkaConsumer] disposing ..."
                consumer.Dispose()
                printfn "    [KafkaConsumer] disposed"

            interface IDisposable with
                member this.Dispose() =
                    this.Close()

    type KafkaMessage = KafkaMessage of ConsumeResult<Ignore, string>

    let private connect (BrokerList brokerList) (Topic topic) =
        let config =
            ConsumerConfig(
                GroupId = (Guid.NewGuid() |> string),
                BootstrapServers = brokerList,
                AutoOffsetReset = (AutoOffsetReset.Earliest |> Nullable),
                EnableAutoCommit = true
            )

        printfn "    [KafkaConsumer] connecting ..."
        let consumer = ConsumerBuilder(config).Build()

        printfn "    [KafkaConsumer] subscribing to topic ..."
        consumer.Subscribe topic

        printfn "    [KafkaConsumer] connected"
        KafkaConsumer consumer

    let private consume (KafkaConsumer consumer) =
        printfn "    [KafkaConsumer] consuming ..."
        let result = consumer.Consume()
        printfn "    [KafkaConsumer] message consumed"

        if isNull result then None
        else Some (KafkaMessage result)

    let consumeSeq brokerList topic =
        seq {
            use consumer = connect brokerList topic

            while true do
                let message = consume consumer

                if message.IsSome then
                    yield message.Value
        }
