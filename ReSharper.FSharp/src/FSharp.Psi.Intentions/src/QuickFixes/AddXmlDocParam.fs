namespace JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.QuickFixes

open System.Collections.Generic
open System.Xml.Linq
open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.Highlightings
open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.QuickFixes
open JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.DocComments
open JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.Tree
open JetBrains.ReSharper.Plugins.FSharp.Psi.Util
open JetBrains.ReSharper.Plugins.FSharp.Psi.PsiUtil
open JetBrains.ReSharper.Psi.ExtensionsAPI
open JetBrains.ReSharper.Psi.ExtensionsAPI.Tree
open JetBrains.ReSharper.Psi.Tree
open JetBrains.ReSharper.Psi.Xml.Impl.Tree
open JetBrains.ReSharper.Psi.Xml.Parsing
open JetBrains.ReSharper.Psi.Xml.XmlDocComments
open JetBrains.ReSharper.Resources.Shell
open JetBrains.Util

type AddXmlDocParam(warning: InvalidXmlDocParamWarning) =
    inherit FSharpQuickFixBase()

    let xmlDocBlock = warning.XmlDocBlock
    let mutable paramsToAdd = []
    let mutable paramsToRemove = []

    override this.IsAvailable _ =
        xmlDocBlock.IsValid() &&
        let expectedParams = XmlDocTemplateUtil.GetParameters(xmlDocBlock.Parent)
        let xml = XDocument.Parse("<doc>\n"+ String.concat "\n" (FSharpDocCommentXmlPsi.GetCommentLines(xmlDocBlock))+"\n</doc>",
                    LoadOptions.SetLineInfo ||| LoadOptions.PreserveWhitespace)
        
        let paramsWithDocs = HashSet()
        paramsToAdd <- []
        paramsToRemove <- []

        for p in xml.Descendants(XName.op_Implicit "param") do
            match p.Attribute(XName.op_Implicit "name") with
            | null -> ()
            | attr ->
                if paramsWithDocs.Add(attr.Value) then ()
                else paramsToRemove <- attr.Value :: paramsToRemove

        for p in expectedParams do
            if not (paramsWithDocs.Contains p) then
                paramsToAdd <- p::paramsToAdd
                
        for p in paramsWithDocs do
            if not (expectedParams |> Seq.contains p) then
                paramsToRemove <- p::paramsToRemove

        not paramsToAdd.IsEmpty || not paramsToRemove.IsEmpty

    override x.Text = "Fix XmlDoc"

    override this.ExecutePsiTransaction _ =
        use writeCookie = WriteLockCookie.Create(xmlDocBlock.IsPhysical())
        use disableFormatter = new DisableCodeFormatter()

        let xmlPsi = xmlDocBlock.GetXmlPsi()
        let Factory = XmlElementFactory.GetInstance(xmlPsi.XmlFile);
        
        let name = "b"
        let tagText = $"<param name=\"{XmlUtil.EscapeXmlString(name)}\"></param>"

        let newTag = Factory.CreateRootTag(tagText);
     
        xmlPsi.AddParameterNodeAfter(newTag,
                                     xmlPsi.GetParameterNodes("a")[0])
        
        for param in paramsToRemove do 
            xmlPsi.RemoveTag(xmlPsi.GetParameterNodes(param)[0])
        ()
