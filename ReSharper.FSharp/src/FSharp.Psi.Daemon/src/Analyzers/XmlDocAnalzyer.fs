namespace JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.Analyzers

open JetBrains.ReSharper.Feature.Services.Daemon
open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.Highlightings
open JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.Tree
open JetBrains.ReSharper.Plugins.FSharp.Psi.Util

[<ElementProblemAnalyzer([| typeof<DocComment> |], HighlightingTypes = [| typeof<InvalidXmlDocPositionWarning> |])>]
type XmlDocAnalyzer() =
    inherit ElementProblemAnalyzer<DocComment>()

    override this.Run(xmlDoc, _, consumer) =
        if xmlDoc.Parent :? XmlDocBlock then () else
        consumer.AddHighlighting(InvalidXmlDocPositionWarning(xmlDoc))


[<ElementProblemAnalyzer([| typeof<XmlDocBlock> |], HighlightingTypes = [| typeof<InvalidXmlDocPositionWarning> |])>]
type XmlDocBlockAnalyzer() =
    inherit ElementProblemAnalyzer<XmlDocBlock>()

    override this.Run(xmlDoc, _, consumer) =
        //if XmlDocTemplateUtil.GetParameters(xmlDoc.Parent) then () else
        //consumer.AddHighlighting(InvalidXmlDocPositionWarning(xmlDoc))
        ()
