module MeetupToGoogle

open FSharp.Data

let icalUrl = "http://www.meetup.com/fr-FR/CARA-Lyon/events/ical/"

let getEvents () =
    let response = Http.RequestStream icalUrl
    let calendar = Ical.Net.Calendar.LoadFromStream(response.ResponseStream)
    calendar 
    |> Seq.collect (fun c -> c.Events)
    |> Seq.iter (fun e -> printfn "%s" e.Description)

open Google.Apis.Services
open Google.Apis.Calendar.v3
open Google.Apis.Auth.OAuth2
open Google.Apis.Util.Store
open System.IO
open System.Threading
open System.Security.Cryptography.X509Certificates
open System

let getCredential () =
    use certificate = new X509Certificate2("MeetupToGoogle-e57f0015de00.p12", "notasecret", X509KeyStorageFlags.Exportable)
    let serviceAccountCredential = ServiceAccountCredential.Initializer("meetuptogoogle@avian-principle-178522.iam.gserviceaccount.com")
    serviceAccountCredential.Scopes <- [CalendarService.Scope.Calendar]
    ServiceAccountCredential(serviceAccountCredential.FromCertificate(certificate))

let baseClientService = BaseClientService.Initializer()
baseClientService.HttpClientInitializer <- getCredential()
baseClientService.ApplicationName <- "MeetupToGoogle"
let calendarService = new CalendarService(baseClientService)

let calendarId = "8hc5n2800f4paesicf2u8610d4@group.calendar.google.com" 

let e = Data.Event()
e.Summary <- "Youhou!!"
e.Start <- Data.EventDateTime()
e.Start.DateTime <- Nullable(DateTime.Now)
e.End <- Data.EventDateTime()
e.End.DateTime <- Nullable(DateTime.Now.AddHours(2.0))
calendarService.Events.Insert(e, calendarId).Execute()

let eventsRequest = calendarService.Events.List(calendarId)
eventsRequest.OrderBy <- Nullable(EventsResource.ListRequest.OrderByEnum.StartTime)
eventsRequest.TimeMin <- Nullable(DateTime.Now)
let events = eventsRequest.Execute()

[<EntryPoint>]
let main argv =
    events.Items |> Seq.iter (fun e -> printfn "%A" e.Summary)
    0 // return an integer exit code
