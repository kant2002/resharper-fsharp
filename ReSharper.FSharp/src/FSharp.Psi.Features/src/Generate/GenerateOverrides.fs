﻿module JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Generate.GenerateOverrides

open System.Collections.Generic
open JetBrains.Application.Settings
open JetBrains.ReSharper.Plugins.FSharp.Psi
open JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.Tree
open JetBrains.ReSharper.Plugins.FSharp.Psi.Parsing
open JetBrains.ReSharper.Plugins.FSharp.Psi.Tree
open JetBrains.ReSharper.Plugins.FSharp.Services.Formatter
open JetBrains.ReSharper.Plugins.FSharp.Util
open JetBrains.ReSharper.Psi.ExtensionsAPI.Tree
open JetBrains.ReSharper.Psi.Tree

let getMembersNeedingTypeAnnotations (mfvInstances: FcsMfvInstance list) =
    let sameParamNumberMembersGroups =
        mfvInstances
        |> List.map (fun mfvInstance -> mfvInstance.Mfv)
        |> List.groupBy (fun mfv ->
            mfv.LogicalName, Seq.map Seq.length mfv.CurriedParameterGroups |> Seq.toList)

    let sameParamNumberMembers =
        List.map snd sameParamNumberMembersGroups

    sameParamNumberMembers
    |> Seq.filter (Seq.length >> ((<) 1))
    |> Seq.concat
    |> HashSet

let generateMember (context: IFSharpTreeNode) displayContext (element: IFSharpGeneratorElement) =
    let mfv = element.Mfv

    let mutable nextUnnamedVariableNumber = 0
    let getUnnamedVariableName () =
        let name = sprintf "var%d" nextUnnamedVariableNumber
        nextUnnamedVariableNumber <- nextUnnamedVariableNumber + 1
        name

    let argNames =
        mfv.CurriedParameterGroups
        |> Seq.map (Seq.map (fun x ->
            let name = x.Name |> Option.defaultWith (fun _ -> getUnnamedVariableName ())
            name, x.Type.Instantiate(element.Substitution)) >> Seq.toList)
        |> Seq.toList

    let typeParams = mfv.GenericParameters |> Seq.map (fun param -> param.Name) |> Seq.toList
    let memberName = mfv.LogicalName

    let factory = context.CreateElementFactory()
    let settingsStore = context.GetSettingsStoreWithEditorConfig()
    let spaceAfterComma = settingsStore.GetValue(fun (key: FSharpFormatSettingsKey) -> key.SpaceAfterComma)
    
    let paramGroups =
        if mfv.IsProperty then [] else
        factory.CreateMemberParamDeclarations(argNames, spaceAfterComma, element.AddTypes, displayContext)

    let memberDeclaration = factory.CreateMemberBindingExpr(memberName, typeParams, paramGroups)

    if element.IsOverride then
        memberDeclaration.SetOverride(true)

    if element.AddTypes then
        let lastParam = memberDeclaration.ParametersPatterns.LastOrDefault()
        if isNull lastParam then () else

        let typeString = mfv.ReturnParameter.Type.Instantiate(element.Substitution)
        let typeUsage = factory.CreateTypeUsage(typeString.Format(displayContext))
        ModificationUtil.AddChildAfter(lastParam, factory.CreateReturnTypeInfo(typeUsage)) |> ignore

    memberDeclaration


let addEmptyLineIfNeeded (anchor: ITreeNode) =
    let addEmptyLine =
        let tokenType = getTokenType anchor
        if tokenType == FSharpTokenType.STRUCT || tokenType == FSharpTokenType.CLASS || tokenType == FSharpTokenType.WITH then false else
        not (anchor :? IMemberDeclaration) || not anchor.IsSingleLine

    if addEmptyLine then
        ModificationUtil.AddChildAfter(anchor, NewLine(anchor.GetLineEnding())) :> ITreeNode
    else
        anchor