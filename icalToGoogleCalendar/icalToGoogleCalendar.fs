module IcalToGoogleCalendar

open FSharp.Data

let icalUrl = "http://www.meetup.com/fr-FR/CARA-Lyon/events/ical/"

let getEvents () =
    let response = Http.RequestStream icalUrl
    let calendar = Ical.Net.Calendar.LoadFromStream(response.ResponseStream)
    calendar 
    |> Seq.collect (fun c -> c.Events)

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
    let serviceAccountCredential = 
        ServiceAccountCredential.Initializer(
            "meetuptogoogle@avian-principle-178522.iam.gserviceaccount.com", 
            Scopes = [CalendarService.Scope.Calendar])
    ServiceAccountCredential(serviceAccountCredential.FromCertificate(certificate))

let getGoogleCalendarService () =
    let baseClientService = BaseClientService.Initializer(
                                HttpClientInitializer = getCredential(),
                                ApplicationName = "MeetupToGoogle")
    new CalendarService(baseClientService)

open System.Collections.Generic
open Ical.Net.Interfaces.Components

type ExistingEvent = {
    GoogleEventId: GoogleEventId
    UpdatedOnSource: DateTime
}
and GoogleEventId = string
type SyncAction = 
    | Create of SourceEvent 
    | Update of SourceEvent * GoogleEventId
    | UpToDate
and SourceEvent = {
    Id: EventId
    Summary: string
    Description: string
    Start: DateTime
    End: DateTime
    UpdatedOnSource: DateTime
}
and EventId = string

let syncEvent (existingGoogleEvents:IDictionary<string, ExistingEvent>) event = 
    if existingGoogleEvents.ContainsKey(event.Id) then
        let existingEvent = existingGoogleEvents.Item(event.Id)
        if existingEvent.UpdatedOnSource.Equals(event.UpdatedOnSource) then
            UpToDate
        else // even if UpdateOnSource is greater on Google than source (manually modified?)
            Update (event, existingEvent.GoogleEventId)
    else
        Create event

let convertSourceEvent (event:IEvent) =
    { Id = event.Uid
      Summary = event.Summary
      Description = event.Description
      Start = event.Start.Value
      End = event.End.Value
      UpdatedOnSource = event.LastModified.Value }

let convertToExistingEvent (googleEvent:Data.Event) =
    let updatedOnSource = 
        if googleEvent.ExtendedProperties <> null
            && googleEvent.ExtendedProperties.Shared.ContainsKey("updatedOnSource") then
            DateTime.Parse(googleEvent.ExtendedProperties.Shared.Item("updatedOnSource"))
        else
            DateTime.MinValue
    { 
        GoogleEventId = googleEvent.Id
        UpdatedOnSource = updatedOnSource
    }

let getExistingGoogleEvents (calendarService:CalendarService) calendarId =
    calendarService.Events.List(
        calendarId, 
        ShowDeleted = Nullable(true),
        MaxResults = Nullable(1000),
        TimeMin = Nullable(DateTime.Now)).Execute().Items
    |> Seq.map (fun e -> e.ICalUID, convertToExistingEvent e)
    |> dict

let convertFromSourceEvent event =
    let extendedProperties = 
        Data.Event.ExtendedPropertiesData(
            Shared = ([("updatedOnSource", event.UpdatedOnSource.ToString())] |> dict))
    Data.Event(
        ICalUID = event.Id,
        Summary = event.Summary,
        Description = event.Description,
        Start = Data.EventDateTime(DateTime = Nullable(event.Start)),
        End = Data.EventDateTime(DateTime = Nullable(event.End)),
        ExtendedProperties = extendedProperties)    

let applySync (calendarService:CalendarService) calendarId syncAction =
    match syncAction with
    | Create e -> 
        let googleEvent = convertFromSourceEvent e
        calendarService.Events.Insert(googleEvent, calendarId).Execute() |> ignore 
    | Update (e, googleEventId) -> 
        let googleEvent = convertFromSourceEvent e
        calendarService.Events.Patch(googleEvent, calendarId, googleEventId).Execute() |> ignore
    | UpToDate -> () 
    syncAction

[<EntryPoint>]
let main argv =
    let calendarId = "8hc5n2800f4paesicf2u8610d4@group.calendar.google.com" 
    let calendarService = getGoogleCalendarService()
    let existingGoogleEvents = getExistingGoogleEvents calendarService calendarId
    getEvents()
    |> Seq.map convertSourceEvent
    |> Seq.map (syncEvent existingGoogleEvents)
    |> Seq.map (applySync calendarService calendarId)
    |> Seq.countBy (function | Create _ -> "created" | Update _ -> "updated" | UpToDate -> "up to date")
    |> Seq.iter (fun x -> printfn "%i %s" (snd x) (fst x))
    0 // return an integer exit code

    let extendedProperties = 
            Data.Event.ExtendedPropertiesData(
                Shared = ([("updatedOnSource", DateTime.Now.AddYears(-1).ToString())] |> dict))
    calendarService.Events.Patch(Data.Event(ExtendedProperties = extendedProperties), calendarId, "_clr6arjkbsp38cph6crj6cpp81mmapbkelo2sorfdk").Execute()