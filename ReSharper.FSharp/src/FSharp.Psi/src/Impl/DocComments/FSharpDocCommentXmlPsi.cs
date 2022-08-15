using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using JetBrains.Diagnostics;
using JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.Tree;
using JetBrains.ReSharper.Plugins.FSharp.Psi.Parsing;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Impl.DocComments;
using JetBrains.ReSharper.Psi.CSharp.Parsing;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.ExtensionsAPI;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.VB;
using JetBrains.ReSharper.Psi.Xml.Impl.CodeStyle;
using JetBrains.ReSharper.Psi.Xml.Tree;
using JetBrains.ReSharper.Psi.Xml.XmlDocComments;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.Util;
using JetBrains.Util.Text;

namespace JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.DocComments
{
  public class FSharpDocCommentElementFactory : ClrDocCommentElementFactoryImpl
  {
    public FSharpDocCommentElementFactory(IDocCommentXmlPsi xmlPsi) : base(xmlPsi)
    {
    }

    protected override Key<object> XmlResolveKey => new("DocCommentXmlPsi.XmlResolveKey");
  }

  public class FSharpDocCommentXmlPsi : ClrDocCommentXmlPsi<XmlDocBlock>
  {
    private FSharpDocCommentXmlPsi(
      [NotNull] InjectedPsiHolderNode docCommentsHolder,
      [NotNull] XmlDocBlock fSharpDocCommentBlock,
      [NotNull] IXmlFile xmlFile, bool isShifted)
      : base(docCommentsHolder, xmlFile, isShifted, fSharpDocCommentBlock)
    {
      var infos = new FSharpDocCommentElementFactory(this).DecodeCRefs(XmlFile);
      BindReferences<IDocCommentReference>(infos);
    }

    [NotNull]
    public static FSharpDocCommentXmlPsi BuildPsi([NotNull] XmlDocBlock block)
    {
      BuildXmlPsi(
        FSharpXmlDocLanguage.Instance.NotNull(), block, GetCommentLines(block),
        out var holderNode, out var xmlPsiFile, out var isShifted);

      return new FSharpDocCommentXmlPsi(holderNode, block, xmlPsiFile, isShifted);
    }

    [NotNull, Pure]
    public static IReadOnlyList<string> GetCommentLines([NotNull] XmlDocBlock block)
    {
      return block.DocComments
        .Select(t => t.CommentText)
        .ToIReadOnlyList();
    }

    protected override IReadOnlyList<ITreeNode> GetDocCommentNodes() => DocCommentBlock.DocComments;


    protected override string GetDocCommentStartText(ITreeNode commentNode) => "///";

    public override void SubTreeChanged()
    {
      // contribute changes to the original tree...
      if (BulkChangesCount > 0)
      {
        SkippedChangesCount++;
        return;
      }

      try
      {
        IsInChange = true;

        var docComments = DocCommentBlock.DocComments;
        if (docComments.Count > 1)
        {
          using (WriteLockCookie.Create(DocCommentBlock.IsPhysical()))
          {
            var firstCommentToDelete = docComments[1];
            var lastCommentToDelete = docComments[^1];
            ModificationUtil.DeleteChildRange(firstCommentToDelete, lastCommentToDelete);
          }
        }

        UpdateIsShifted();

        using (new DisableCodeFormatter())
        {
          var lines = XmlFile.GetText().SplitByNewLine();
          var shiftIndent = IsShifted ? " " : "";

          CreateDocCommentPerLine();

          void CreateDocCommentPerLine()
          {
            var first = true;

            foreach (var line in lines)
            {
              var comment = FSharpComment.CreateDocComment(shiftIndent + line);

              if (first)
              {
                DocCommentBlock.DocComments[0].ReplaceBy(comment);
                first = false;
              }
              else
              {
                DocCommentBlock.AddDocCommentBefore(comment, anchor: null);
              }
            }
          }
        }
        XmlDocFormatterImplHelper.FormatDocComment(DocCommentBlock);

        InvalidateCachedData();
      }
      finally
      {
        IsInChange = false;
      }
    }
  }
}
