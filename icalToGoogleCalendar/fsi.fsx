#r "../packages/FSharp.Data/lib/portable-net45+netcore45/FSharp.Data.dll"
#r "../packages/Google.Apis/lib/netstandard1.3/Google.Apis.dll"
#r "../packages/Google.Apis.Auth/lib/netstandard1.3/Google.Apis.Auth.dll"
#r "../packages/Google.Apis.Calendar.v3/lib/portable-net45+sl50+netcore45+wpa81+wp8/Google.Apis.Calendar.v3.dll"
#r "../packages/Google.Apis.Core/lib/netstandard1.3/Google.Apis.Core.dll"
#r "../packages/Ical.Net/lib/net46/Ical.Net.dll"
#r "../packages/Ical.Net/lib/net46/Ical.Net.Collections.dll"
#r "../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../packages/System.Security.Cryptography.X509Certificates/lib/net461/System.Security.Cryptography.X509Certificates.dll"
#r "../packages/System.Security.Cryptography.Algorithms/lib/net461/System.Security.Cryptography.Algorithms.dll"
#r "../packages/System.Security.Cryptography.Primitives/lib/net46/System.Security.Cryptography.Primitives.dll"
#r "../packages/System.Reflection.TypeExtensions/lib/net461/System.Reflection.TypeExtensions.dll"
#r "../packages/System.Runtime.Serialization.Formatters/lib/netstandard1.4/System.Runtime.Serialization.Formatters.dll"
#r "../packages/System.ComponentModel.TypeConverter/lib/netstandard1.5/System.ComponentModel.TypeConverter.dll"

#load "icalToGoogleCalendar.fs"

// Just to test update: it allows to set updatedOnSource property in the past
open Google.Apis.Calendar.v3
open System
let calendarId = "8hc5n2800f4paesicf2u8610d4@group.calendar.google.com"
let calendarService = IcalToGoogleCalendar.getGoogleCalendarService ()
let extendedProperties = 
        Data.Event.ExtendedPropertiesData(
            Shared = ([("updatedOnSource", DateTime.Now.AddYears(-1).ToString())] |> dict))
calendarService.Events.Patch(Data.Event(ExtendedProperties = extendedProperties), calendarId, "_clr6arjkbsp38cph6crj6cpp81mmapbkelo2sorfdk").Execute()