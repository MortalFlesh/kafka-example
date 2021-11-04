open System
open MF.Kafka

let getEnv defaultValue name =
    match Environment.GetEnvironmentVariable name with
    | null | "" -> defaultValue
    | value -> value

[<EntryPoint>]
let main argv =
    printfn "[Main] Kafka - Example"

    let brokerList = BrokerList (getEnv "" "KAFKA_BROKER_LIST")
    let topic = Topic (getEnv "" "KAFKA_TOPIC")

    printfn "[Main] Kafka - configuration: \n%A\n" [
        $"{brokerList}"
        $"{topic}"
    ]

    let messages = [1..5] |> List.map (sprintf "Message %i")

    let produce () =
        printfn "[Produce] connect producer ..."
        use producer = Producer.connect brokerList

        printfn "[Produce] produce messages ..."
        messages
        |> List.map (fun message -> async {
            message |> Producer.produce producer topic

            printfn "[Produce] waiting ..."
            do! Async.Sleep (2000)
        })
        |> Async.Sequential
        |> Async.RunSynchronously
        |> ignore

    let consume () =
        printfn "[Consume] consume messages ..."
        Consumer.consumeSeq brokerList topic
        |> Seq.take messages.Length
        |> List.ofSeq

    printfn "[Main] Run example:\n-------------------"
    produce()
    let consumed = consume()

    printfn "[Main] Messages (produced -> consumed)"
    consumed
    |> List.iter (fun (Consumer.KafkaMessage message) -> printfn " - %s" message.Message.Value)

    printfn "\n[Main] done"
    0
