﻿namespace RSXmlCombiner.FuncUI

module TrackList =
    open System
    open System.IO
    open Elmish
    open Avalonia.Media
    open Avalonia.Controls
    open Avalonia.Controls.Shapes
    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI.Types
    open Avalonia.Layout
    open Avalonia.Input
    open XmlUtils

    type Msg =
    | RemoveTrackAt of index : int
    | ChangeAudioFile of trackIndex : int
    | ChangeAudioFileResult of trackIndex : int * newFile:string[]
    | SelectArrangementFile of trackIndex : int * arrIndex : int
    | ChangeArrangementFile of trackIndex : int * arrIndex : int * string[]
    | ArrangementBaseToneChanged of trackIndex : int * arrIndex : int * toneIndex : int
    | RemoveArrangement of trackIndex : int * arrIndex : int
    | ShowReplacementToneEditor of trackIndex : int * arrIndex : int
    | TrimAmountChanged of trackIndex : int * trimAmunt : double

    let private changeAudioFile track newFile = { track with AudioFile = Some newFile }

    /// Updates the model according to the message content.
    let update (msg: Msg) (state: ProgramState) =
        match msg with
        | RemoveTrackAt index ->
            { state with Tracks = state.Tracks |> List.except [ state.Tracks.[index] ] }, Cmd.none

        | ChangeAudioFile trackIndex ->
            let initialDir = state.Tracks.[trackIndex].AudioFile |> Option.map Path.GetDirectoryName
            let selectFiles = Dialogs.openFileDialog "Select Audio File" Dialogs.audioFileFilters false initialDir
            state, Cmd.OfAsync.perform (fun _ -> selectFiles) trackIndex (fun files -> ChangeAudioFileResult(trackIndex, files))

        | ChangeAudioFileResult (trackIndex, files) ->
            if files.Length > 0 then
                let fileName = files.[0]
                let newTracks = state.Tracks |> List.mapi (fun i t -> if i = trackIndex then changeAudioFile t fileName else t) 
                { state with Tracks = newTracks }, Cmd.none
            else
                state, Cmd.none

        | SelectArrangementFile (trackIndex, arrIndex) ->
            let initialDir = state.Tracks.[trackIndex].Arrangements.[arrIndex].FileName |> Option.map Path.GetDirectoryName
            let files = Dialogs.openFileDialog "Select Arrangement File" Dialogs.xmlFileFilter false initialDir
            state, Cmd.OfAsync.perform (fun _ -> files) () (fun f -> ChangeArrangementFile (trackIndex, arrIndex, f))

        | ChangeArrangementFile (trackIndex, arrIndex, files) ->
            if files.Length > 0 then
                let fileName = files.[0]
                let rootName = XmlHelper.GetRootElementName(fileName)
                let arrangement = state.Tracks.[trackIndex].Arrangements.[arrIndex]

                match rootName, arrangement.ArrangementType with
                // For instrumental arrangements, create an arrangement from the file, preserving the arrangement type and name
                | "song", t when isInstrumental t ->
                    let newArr = { createInstrumental fileName (Some t) with Name = arrangement.Name }
                    let updatedTracks = updateSingleArrangement state.Tracks trackIndex arrIndex newArr
                    { state with Tracks = updatedTracks }, Cmd.none

                // For vocals and show lights, just change the file name
                | "vocals", ArrangementType.Vocals
                | "vocals", ArrangementType.JVocals
                | "showlights", ArrangementType.ShowLights ->
                    let newArr = { arrangement with FileName = Some fileName }
                    let updatedTracks = updateSingleArrangement state.Tracks trackIndex arrIndex newArr
                    { state with Tracks = updatedTracks }, Cmd.none

                | _ -> { state with StatusMessage = "Incorrect arrangement type" }, Cmd.none
            else
                state, Cmd.none

        | ArrangementBaseToneChanged (trackIndex, arrIndex, toneIndex) ->
            match state.Tracks.[trackIndex].Arrangements.[arrIndex] with
            | { Data = Some arrData } as arrangement ->
                let data = { arrData with BaseToneIndex = toneIndex }

                let newArr = { arrangement with Data = Some data }
                let updatedTracks = updateSingleArrangement state.Tracks trackIndex arrIndex newArr

                { state with Tracks = updatedTracks }, Cmd.none

            | { Data = None } ->
                // Should not be able to get here
                { state with StatusMessage = "Critical program error." }, Cmd.none

        | RemoveArrangement (trackIndex, arrIndex) ->
            let newArr =
                { state.Tracks.[trackIndex].Arrangements.[arrIndex] with FileName = None; Data = None }

            let updatedTracks = updateSingleArrangement state.Tracks trackIndex arrIndex newArr

            { state with Tracks = updatedTracks }, Cmd.none

        | ShowReplacementToneEditor (trackIndex, arrIndex) ->
            { state with ReplacementToneEditor = Some(trackIndex, arrIndex) }, Cmd.none

        | TrimAmountChanged (trackIndex, trimAmount) ->
            let trim = int (Math.Round(trimAmount * 1000.0))
            let newTracks = state.Tracks |> List.mapi (fun i t -> if i = trackIndex then { t with TrimAmount = trim } else t) 
            { state with Tracks = newTracks }, Cmd.none

    /// Creates the view for an arrangement.
    let private arrangementView (arr : Arrangement) trackIndex arrIndex state dispatch =
        let fileName = arr.FileName
        let fileNameBrush =
            match fileName with
            | Some fn when not <| File.Exists(fn) -> Brushes.Red
            | _ -> Brushes.DarkGray

        let color =
            match fileName with
            | Some ->
                match arr.ArrangementType with
                | ArrangementType.Lead -> CustomBrushes.lead
                | ArrangementType.Rhythm | ArrangementType.Combo -> CustomBrushes.rhythm
                | ArrangementType.Bass -> CustomBrushes.bass
                | ArrangementType.Vocals | ArrangementType.JVocals -> Brushes.Yellow
                | ArrangementType.ShowLights -> Brushes.Violet
                | _ -> Brushes.GhostWhite
            | None -> Brushes.Gray

        Border.create [
            Border.borderThickness 1.0
            Border.borderBrush color
            Border.minWidth 140.0
            Border.classes [ "arrangement" ]
            Border.child (
                StackPanel.create [
                    StackPanel.verticalAlignment VerticalAlignment.Top
                    StackPanel.classes [ "arrangement" ]
                    StackPanel.children [
                        // Header
                        yield Grid.create [
                            Grid.columnDefinitions "auto,auto,*"
                            Grid.children [
                                // Arrangement Icon
                                Path.create [
                                    Path.fill color
                                    Path.data (
                                        match arr.ArrangementType with
                                        | t when isInstrumental t -> Icons.pick
                                        | t when isVocals t -> Icons.microphone
                                        | _ -> Icons.spotlight)
                                ]
                                // Arrangement name
                                TextBlock.create [
                                    Grid.column 1
                                    TextBlock.margin (4.0, 0.0, 0.0, 0.0 )
                                    TextBlock.classes [ "h2"]
                                    TextBlock.text arr.Name
                                    TextBlock.foreground color
                                    TextBlock.cursor <| Cursor StandardCursorType.Hand
                                    TextBlock.onTapped (fun _ -> SelectArrangementFile(trackIndex, arrIndex) |> dispatch)
                                ]
                                // Remove arrangement file button
                                ContentControl.create [
                                    Grid.column 2
                                    ContentControl.horizontalAlignment HorizontalAlignment.Right
                                    ContentControl.width 22.0
                                    ContentControl.height 22.0
                                    ContentControl.content (
                                        Canvas.create [
                                            yield Canvas.width 22.0
                                            yield Canvas.height 22.0
                                            yield Canvas.classes [ "removeArr" ]
                                            // If there is no file set, always hide the remove button
                                            if arr.FileName |> Option.isNone then yield Canvas.isVisible false
                                            yield Canvas.onTapped (fun _ -> RemoveArrangement(trackIndex, arrIndex) |> dispatch)
                                            yield Canvas.children [
                                                Path.create [
                                                    Path.fill Brushes.DarkRed
                                                    Path.data Icons.close
                                                ]
                                            ]
                                        ]
                                    )
                                ]
                            ]
                        ]
                        // File Name
                        yield TextBlock.create [
                            yield TextBlock.text (
                                match fileName with
                                | Some fn -> Path.GetFileNameWithoutExtension(fn)
                                | None -> "No file")
                            yield TextBlock.width 100.0
                            yield TextBlock.foreground fileNameBrush
                            yield TextBlock.cursor <| Cursor StandardCursorType.Hand
                            yield TextBlock.onTapped (fun _ -> SelectArrangementFile(trackIndex, arrIndex) |> dispatch)
                            yield ToolTip.tip (fileName |> Option.defaultValue "Click to select a file.")
                        ]

                        // Optional Tone Controls
                        match arr.Data with
                        | None -> () // Do nothing
                        | Some instArr ->
                            let baseToneNames = ProgramState.getReplacementToneNames arr.Name state.CommonTones

                            // The selection on the combo box is lost if the tone name at that index is edited in the common tone editor
                            // As a workaround, yield the combo box only when the project view is active
                            if instArr.ToneNames.Length = 0 && trackIndex <> 0 && state.ProjectViewActive then
                                // Base Tone Combo Box
                                yield ComboBox.create [
                                    ComboBox.width 100.0
                                    ComboBox.height 30.0
                                    ComboBox.margin (0.0, 5.0) 
                                    ComboBox.dataItems baseToneNames
                                    ComboBox.selectedIndex instArr.BaseToneIndex
                                    ComboBox.onSelectedIndexChanged (fun toneIndex -> if toneIndex <> -1 then ArrangementBaseToneChanged(trackIndex, arrIndex, toneIndex) |> dispatch)
                                    ToolTip.tip "Base Tone"
                                ]
                            else if instArr.ToneNames.Length > 0 then
                                // Edit Replacement Tones Button
                                yield Button.create [
                                    Button.content "Tones"
                                    Button.width 100.0
                                    Button.margin (0.0, 5.0)
                                    Button.onClick (fun _ -> ShowReplacementToneEditor(trackIndex, arrIndex) |> dispatch)
                                    Button.borderThickness 0.0
                                    Button.background (
                                        if instArr.ToneReplacements.IsEmpty 
                                           || instArr.ToneReplacements |> Map.exists (fun _ ti -> ti = -1 || ti >= baseToneNames.Length) then
                                            Brushes.DarkRed
                                        else
                                            Brushes.DarkGreen
                                    )
                                ]
                    ]
                ]
            )
        ]
       
    /// Creates the view for a track.
    let private trackView (track : Track) index state dispatch =
        let audioFileBrush =
            match track.AudioFile with
            | Some fn when not <| File.Exists(fn) -> Brushes.Red
            | _ -> Brushes.DarkGray

        Border.create [
            Border.classes [ "track" ]
            Border.child (
                DockPanel.create [
                    DockPanel.margin (5.0, 0.0, 0.0, 0.0)
                    DockPanel.children [
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.dock Dock.Top
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.text (sprintf "%i. %s" (index + 1) track.Title)
                                    TextBlock.classes [ "h1" ]
                                ]
                            ]
                        ]
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                // Delete button
                                //Path.create [
                                //    Path.data Icons.close
                                //    Path.fill "Red"
                                //    Path.margin (0.0, 0.0, 20.0, 0.0)
                                //    Path.verticalAlignment VerticalAlignment.Center
                                //    Path.onTapped (fun _ -> dispatch (RemoveTrackAt index))
                                //    Path.classes [ "close" ]
                                //    Path.cursor (Cursor(StandardCursorType.Hand))
                                //    Path.renderTransform (ScaleTransform(1.5, 1.5))
                                //]
    
                                Button.create [
                                    Button.verticalAlignment VerticalAlignment.Center
                                    Button.fontSize 18.0
                                    Button.margin (2.0, 0.0, 5.0, 0.0)
                                    Button.content "X"
                                    Button.classes [ "close" ]
                                    Button.onClick (fun _ -> RemoveTrackAt index |> dispatch)
                                ]

                                // Audio Part
                                StackPanel.create [
                                    StackPanel.width 150.0
                                    StackPanel.classes [ "part" ]
                                    StackPanel.children [
                                        TextBlock.create [
                                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                                            TextBlock.text "Audio"
                                            TextBlock.classes [ "h2" ]
                                        ]
                                        // Audio File Name
                                        TextBlock.create [
                                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                                            TextBlock.foreground audioFileBrush
                                            TextBlock.maxWidth 100.0
                                            TextBlock.cursor <| Cursor StandardCursorType.Hand
                                            TextBlock.onTapped (fun _ -> ChangeAudioFile index |> dispatch)
                                            TextBlock.text (track.AudioFile |> Option.defaultValue "None selected" |> Path.GetFileName)
                                            ToolTip.tip (track.AudioFile |> Option.defaultValue "Click to select a file.")
                                        ]

                                        // Trim Part
                                        StackPanel.create [
                                            StackPanel.classes [ "part" ]
                                            StackPanel.orientation Orientation.Horizontal
                                            StackPanel.horizontalAlignment HorizontalAlignment.Center
                                            StackPanel.children [
                                                // Hide if this is the first track
                                                if index <> 0 then
                                                    yield TextBlock.create [ 
                                                        TextBlock.classes [ "h2" ]
                                                        TextBlock.text "Trim:"
                                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                                    ]
                                                    yield NumericUpDown.create [
                                                        NumericUpDown.value <| (double track.TrimAmount) / 1000.0
                                                        NumericUpDown.minimum 0.0
                                                        NumericUpDown.verticalAlignment VerticalAlignment.Center
                                                        NumericUpDown.width 75.0
                                                        NumericUpDown.formatString "F3"
                                                        NumericUpDown.onValueChanged (fun trim -> TrimAmountChanged(index, trim) |> dispatch)
                                                        ToolTip.tip "Sets the amount of time in seconds to be trimmed from the start of the audio and each arrangements."
                                                    ]
                                                    yield TextBlock.create [
                                                        TextBlock.margin (2.0, 0.0, 0.0, 0.0)
                                                        TextBlock.text "s"
                                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                                    ]
                                            ]
                                        ]
                                    ]
                                ]

                                // Arrangements
                                StackPanel.create [
                                    StackPanel.orientation Orientation.Horizontal
                                    StackPanel.spacing 10.0
                                    StackPanel.children <| List.mapi (fun i item -> arrangementView item index i state dispatch :> IView) track.Arrangements
                                ]
                            ]
                        ]
                    ]
                ]
            )
        ]

    /// Creates the track list view.
    let view state dispatch =
        // List of tracks
        ScrollViewer.create [
            ScrollViewer.horizontalScrollBarVisibility Primitives.ScrollBarVisibility.Auto
            ScrollViewer.content (
                StackPanel.create [
                    StackPanel.children <| List.mapi (fun i item -> trackView item i state dispatch :> IView) state.Tracks
                ] 
            )
        ]