﻿namespace RSXmlCombiner.FuncUI

open Elmish
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Input
open Avalonia.FuncUI
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Components.Hosts

type MainWindow() as this =
    inherit HostWindow()
    do
        base.Title <- "Rocksmith 2014 XML Combiner"
        base.Width <- 1160.0
        base.Height <- 800.0
        base.MinWidth <- 900.0
        base.MinHeight <- 450.0

        let handleHotkeys dispatch (event : KeyEventArgs) =
            let dispatch = Shell.Msg.TopControlsMsg >> dispatch
            match event.KeyModifiers, event.Key with
            | KeyModifiers.Control, Key.O -> dispatch TopControls.Msg.SelectOpenProjectFile
            | KeyModifiers.Control, Key.S -> dispatch TopControls.Msg.SelectSaveProjectFile
            | KeyModifiers.Control, Key.N -> dispatch TopControls.Msg.NewProject
            | _ -> ()

        let audioCombinerProgress _initialModel =
            let sub dispatch =
                AudioCombiner.progress.ProgressChanged.Add(fun x -> Shell.Msg.CombineAudioProgressChanged x |> dispatch)
            Cmd.ofSub sub

        let arrangementCombinerProgress _initialModel =
            let sub dispatch =
                ArrangementCombiner.progress.ProgressChanged.Add(fun x -> Shell.Msg.CombineArrangementsProgressChanged x |> dispatch)
            Cmd.ofSub sub
        
        let hotKeysSub _initialModel =
            Cmd.ofSub (fun dispatch -> this.KeyDown.Add(handleHotkeys dispatch))

        //this.VisualRoot.VisualRoot.Renderer.DrawFps <- true
        //this.VisualRoot.VisualRoot.Renderer.DrawDirtyRects <- true

        Elmish.Program.mkProgram Shell.init Shell.update Shell.view
        |> Program.withHost this
        |> Program.withSubscription audioCombinerProgress
        |> Program.withSubscription arrangementCombinerProgress
        |> Program.withSubscription hotKeysSub
        |> Program.run

        
type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Load "avares://Avalonia.Themes.Default/DefaultTheme.xaml"
        this.Styles.Load "avares://Avalonia.Themes.Default/Accents/BaseDark.xaml"
        this.Styles.Load "avares://RSXmlCombiner/Styles.xaml"

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow()
        | _ -> ()

module Program =
    [<EntryPoint>]
    let main(args: string[]) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
