using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var inputs = JsonSerializer.Deserialize<List<Input>>(Console.In.ReadToEnd())!;
var sourcePatterns = new Dictionary<string,string> {
 ["catalog"] = @"OfficialCardCatalog|CardCatalog|card-catalog|data/official",
 ["functional_groups"] = @"FunctionalUnitBuilder",
 ["normal_registry"] = @"CardBehaviorRegistry",
 ["input_identity"] = @"RuleInputs?|Cn16|CatalogIdentity|CatalogSha256|SourceInput",
 ["source_fixture"] = @"CanonicalSource|OfficialSource|Historical.*(?:Catalog|Source)|Prior.*(?:Catalog|Group|Member)"
};
var gameplay = new Regex(@"\b(?:new\s+(?:[A-Za-z0-9_]*Command|MatchSession|PlayerIntent)|(?:Act|ActRetained|Top|Replay|ResolveAsync|SubmitAsync|Submit|Cold|Registry|PreviewPlayCard|PreviewPlayCardAsync|SendAsync)\s*\()", RegexOptions.CultureInvariant);
string TypeKey(SyntaxNode n) => string.Join(".", n.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(x=>x.Name.ToString()).Concat(n.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(x=>x.Identifier.ValueText)));
string[] Markers(string text) => sourcePatterns.Where(p=>Regex.IsMatch(text,p.Value,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)).Select(p=>p.Key).ToArray();
object[] Calls(SyntaxNode n) => n.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(x=>x.Expression switch {
 IdentifierNameSyntax id => (object)new { member=id.Identifier.ValueText, receiver="" },
 GenericNameSyntax g => new { member=g.Identifier.ValueText, receiver="" },
 MemberAccessExpressionSyntax m => new { member=m.Name.Identifier.ValueText, receiver=m.Expression.ToString() },
 _ => new { member="", receiver=x.Expression.ToString() }
}).ToArray();
var nodes = new List<object>();
var classes = new Dictionary<string,object>();
var diagnostics = new List<object>();
foreach(var input in inputs) {
 var tree = CSharpSyntaxTree.ParseText(input.code, new CSharpParseOptions(LanguageVersion.Preview),input.path);
 var root = tree.GetRoot();
 foreach(var d in tree.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error)) diagnostics.Add(new { input.path, message=d.ToString() });
 var staticUsings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().Where(u=>!u.StaticKeyword.IsKind(SyntaxKind.None)).Select(u=>u.Name!.ToString()).ToArray();
 foreach(var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>()) {
  var key = string.Join(".", type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(x=>x.Name.ToString()).Concat(type.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Reverse().Select(x=>x.Identifier.ValueText)));
  var nonMethods = string.Join("\n",type.Members.Where(m=>m is not MethodDeclarationSyntax && m is not TypeDeclarationSyntax).Select(m=>m.ToFullString()));
  classes[key+"@"+input.path]=new { key, input.path, markers=Markers(nonMethods), gameplay=gameplay.IsMatch(nonMethods), staticUsings };
 }
 foreach(var m in root.DescendantNodes().OfType<MethodDeclarationSyntax>()) {
  var owner=TypeKey(m); if(owner.Length==0)continue;
  var attrs=m.AttributeLists.SelectMany(a=>a.Attributes).Select(a=>a.Name.ToString().Split('.').Last()).ToArray();
  var isTest=attrs.Any(a=>a is "Fact" or "Theory" or "FactAttribute" or "TheoryAttribute");
  var text=(m.Body?.ToFullString() ?? m.ExpressionBody?.ToFullString() ?? "")+string.Join(" ",m.AttributeLists.Select(a=>a.ToFullString()));
  nodes.Add(new { key=owner+"."+m.Identifier.ValueText, owner, name=m.Identifier.ValueText, input.path, line=m.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line+1, isTest, attributes=attrs, markers=Markers(text), gameplay=gameplay.IsMatch(text), calls=Calls(m), staticUsings });
 }
 foreach(var p in root.DescendantNodes().OfType<PropertyDeclarationSyntax>()) {
  var owner=TypeKey(p); if(owner.Length==0)continue;
  var text=p.ToFullString();nodes.Add(new { key=owner+"."+p.Identifier.ValueText, owner, name=p.Identifier.ValueText, input.path, line=p.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line+1, isTest=false, attributes=Array.Empty<string>(), markers=Markers(text), gameplay=gameplay.IsMatch(text), calls=Calls(p), staticUsings });
 }
}
Console.Write(JsonSerializer.Serialize(new { nodes,classes=classes.Values,diagnostics }));
record Input(string path,string code);
