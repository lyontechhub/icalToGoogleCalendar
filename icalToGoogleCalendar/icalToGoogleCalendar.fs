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

type SyncAction = Create of Event | Update of Event | UpToDate
and Event = {
    Id: EventId
    Summary: string
    Description: string
    Start: DateTime
    End: DateTime
    UpdatedOnSource: DateTime
}
and EventId = string
type ExistingEvent = {
    Id: EventId
    UpdatedOnSource: DateTime
}

let syncEvent (existingGoogleEvents:IDictionary<string, ExistingEvent>) (event:Event) = 
    if existingGoogleEvents.ContainsKey(event.Id) then
        let existingEvent = existingGoogleEvents.Item(event.Id)
        if existingEvent.UpdatedOnSource.Equals(event.UpdatedOnSource) then
            UpToDate
        else // even if UpdateOnSource is greater on Google than source (manually modified?)
            Update event
    else
        Create event

let convertSourceEvent (event:IEvent) =
    { Id = event.Uid
      Summary = event.Summary
      Description = event.Description
      Start = event.Start.Value
      End = event.End.Value
      UpdatedOnSource = event.LastModified.Value }

let convertExistingEvent (googleEvent:Data.Event) =
    let updatedOnSource = 
        if googleEvent.ExtendedProperties <> null
            && googleEvent.ExtendedProperties.Shared.ContainsKey("updatedOnSource") then
            DateTime.Parse(googleEvent.ExtendedProperties.Shared.Item("updatedOnSource"))
        else
            DateTime.MinValue
    { 
        Id = googleEvent.ICalUID
        UpdatedOnSource = updatedOnSource
    }

let getExistingGoogleEvents (calendarService:CalendarService) calendarId =
    calendarService.Events.List(
        calendarId, 
        ShowDeleted = Nullable(true),
        MaxResults = Nullable(1000),
        TimeMin = Nullable(DateTime.Now)).Execute().Items
    |> Seq.map (fun e -> e.ICalUID, convertExistingEvent e)
    |> dict

let applySync (calendarService:CalendarService) calendarId syncAction =
    match syncAction with
    | Create e -> 
        let extendedProperties = 
            Data.Event.ExtendedPropertiesData(
                Shared = ([("updatedOnSource", e.UpdatedOnSource.ToString())] |> dict))
        let googleEvent = Data.Event(
                            ICalUID = e.Id,
                            Summary = e.Summary,
                            Description = e.Description,
                            Start = Data.EventDateTime(DateTime = Nullable(e.Start)),
                            End = Data.EventDateTime(DateTime = Nullable(e.End)),
                            ExtendedProperties = extendedProperties)
        calendarService.Events.Insert(googleEvent, calendarId).Execute() |> ignore 
    | Update e -> ()
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

    // let extendedProperties = 
    //         Data.Event.ExtendedPropertiesData(
    //             Shared = ([("updatedOnSource", DateTime.Now.AddYears(-1).ToString())] |> dict))
    // calendarService.Events.Patch(Data.Event(ExtendedProperties = extendedProperties), calendarId, "_clr6arjkbsp38cho64sj4dho81mmapbkelo2sorfdk")