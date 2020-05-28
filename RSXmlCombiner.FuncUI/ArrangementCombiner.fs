﻿namespace RSXmlCombiner.FuncUI

module ArrangementCombiner =
    open System
    open System.IO
    open Rocksmith2014Xml
    open XmlCombiners
    open Types

    /// Combines the show light arrangements if all tracks have one set.
    let private combineShowLights tracks index targetFolder =
        if tracks |> List.forall (fun t -> t.Arrangements.[index].FileName |> Option.isSome) then
            let combiner = ShowLightsCombiner()
            for track in tracks do
                let next = ShowLights.Load(track.Arrangements.[index].FileName |> Option.get)
                combiner.AddNext(next, track.SongLength, track.TrimAmount)

            combiner.Save(Path.Combine(targetFolder, "Combined_Showlights_RS2.xml"))

    /// Inserts the given title at the beginning of the given vocals arrangement.
    let private addTitle (vocals : Vocals) (title : string) (startBeat : float32) =
        let mutable displayTime = 3.0f
        let startTime = startBeat

        if vocals.Count > 0 && vocals.[0].Time < startTime + displayTime then
            displayTime <- startTime - vocals.[0].Time - 0.1f

        // Don't add the title if it will be displayed for less than half a second
        if displayTime > 0.5f then
            let words = title.Split(' ')
            let length = displayTime / (float32 words.Length)
            for i = words.Length - 1 downto 0 do
                vocals.Insert(0, Vocal(startTime + (length * (float32 i)), length, words.[i]))

    // Combines the vocals arrangements if at least one track has one.
    let private combineVocals (tracks : Track list) index targetFolder addTitles =
        // TODO: Always generate lyrics file if addTitles is true?
        if tracks |> List.exists (fun t -> t.Arrangements.[index].FileName |> Option.isSome) then
            let combiner = VocalsCombiner()
            for (trackIndex, track) in tracks |> Seq.indexed do
                let next = 
                    match track.Arrangements.[index].FileName with
                    | Some fn -> Vocals.Load(fn)
                    | None -> Vocals()
                
                if addTitles then
                    let title = sprintf "%i. %s+" (trackIndex + 1) track.Title
                    addTitle next title track.TrimAmount

                combiner.AddNext(next, track.SongLength, track.TrimAmount)

            combiner.Save(Path.Combine(targetFolder, sprintf "Combined_%s_RS2.xml" tracks.[0].Arrangements.[index].Name))

    let private replaceToneNames (song : RS2014Song) (toneReplacements : Map<string, string>) =
        // Replace the tone names of the defined tones and the tone changes
        for kv in toneReplacements do
            if song.ToneBase = kv.Key then song.ToneBase <- kv.Value
            if song.ToneA = kv.Key then song.ToneA <- kv.Value
            if song.ToneB = kv.Key then song.ToneB <- kv.Value
            if song.ToneC = kv.Key then song.ToneC <- kv.Value
            if song.ToneD = kv.Key then song.ToneD <- kv.Value

            if not (song.ToneChanges |> isNull) then
                for tone in song.ToneChanges do
                    if tone.Name = kv.Key then
                        tone.Name <- kv.Value

        // Make sure that there are no duplicate names in the defined tones
        let mutable uniqueTones : Set<string> = Set.empty
        if not (song.ToneB |> isNull) then uniqueTones <- uniqueTones.Add song.ToneB
        if not (song.ToneC |> isNull) then uniqueTones <- uniqueTones.Add song.ToneC
        if not (song.ToneD |> isNull) then uniqueTones <- uniqueTones.Add song.ToneD
        if not (song.ToneA |> isNull) then uniqueTones <- uniqueTones.Remove song.ToneA

        song.ToneB <- null
        song.ToneC <- null
        song.ToneD <- null

        // Set the properties using reflection
        let mutable toneChar = 'B'
        let songType = song.GetType()
        for toneName in uniqueTones do
            let toneProp = songType.GetProperty(sprintf "Tone%c" toneChar)
            toneProp.SetValue(song, toneName)
            toneChar <- toneChar + char 1

    /// Combines the instrumental arrangements at the given index if all tracks have one set.
    let private combineInstrumental (tracks : Track list) index targetFolder combinedTitle coercePhrases =
        let arrType = tracks.[0].Arrangements.[index].ArrangementType

        if tracks |> List.forall (fun t -> t.Arrangements.[index].FileName |> Option.isSome) then
            let combiner = InstrumentalCombiner()

            for i = 0 to tracks.Length - 1 do
                let arr = tracks.[i].Arrangements.[index]
                let arrData = arr.Data |> Option.get
                let next = RS2014Song.Load(arr.FileName |> Option.get)

                if not arrData.ToneNames.IsEmpty then
                    replaceToneNames next arrData.ToneReplacements
                else
                    next.ToneBase <- arrData.BaseTone |> Option.get

                combiner.AddNext(next, tracks.[i].TrimAmount, (i = tracks.Length - 1))

            if not (String.IsNullOrEmpty(combinedTitle)) then
                combiner.SetTitle(combinedTitle)

            combiner.Save(Path.Combine(targetFolder, sprintf "Combined_%s_RS2.xml" (arrType.ToString())), coercePhrases)

    /// Combines all the arrangements in the project.
    let combineArrangements project targetFolder  =
        let nArrangements = project.Tracks.Head.Arrangements.Length
        for i in 0..nArrangements - 1 do
            match project.Tracks.Head.Arrangements.[i].ArrangementType with
            | ArrangementType.Lead | ArrangementType.Rhythm | ArrangementType.Combo | ArrangementType.Bass ->
                combineInstrumental project.Tracks i targetFolder project.CombinationTitle project.CoercePhrases
            | ArrangementType.Vocals | ArrangementType.JVocals ->
                combineVocals project.Tracks i targetFolder project.AddTrackNamesToLyrics
            | ArrangementType.ShowLights -> 
                combineShowLights project.Tracks i targetFolder
            | _ -> failwith "Unknown arrangement type."