using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using COSGeniusSystem.Services.Herbalist;
using COSGeniusSystem.DTOs;
var checks=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
var services = new ServiceCollection();
services.AddLogging();
services.AddMvc();
using var provider = services.BuildServiceProvider();
bool Valid(object value)
{
    var context = new ActionContext(new DefaultHttpContext { RequestServices = provider },
        new RouteData(), new ActionDescriptor());
    provider.GetRequiredService<IObjectModelValidator>().Validate(context, null, "", value);
    return context.ModelState.IsValid;
}
Check(HerbalistWorkflowRules.UsableReceipt(100m,null)==0m,"Unchecked line cannot supply a prescription");
Check(HerbalistWorkflowRules.UsableReceipt(100m,0m)==0m,"Checked zero cannot supply a prescription");
Check(HerbalistWorkflowRules.UsableReceipt(100m,80m)==80m,"Short receipt limits allocation");
Check(HerbalistWorkflowRules.UsableReceipt(100m,120m)==100m,"Unresolved surplus is excluded");
Check(HerbalistWorkflowRules.FitsReceipt(100m,80m,30m,50m),"Two prescriptions can share one received line");
Check(!HerbalistWorkflowRules.FitsReceipt(100m,80m,30m,50.01m),"Combined allocations cannot exceed actual receipt");
Check(!HerbalistWorkflowRules.FitsReceipt(100m,120m,100m,0.01m),"Surplus cannot fund an extra prescription");
Check(!HerbalistWorkflowRules.FitsReceipt(100m,null,0m,1m),"NULL is not full receipt");
Check(!HerbalistWorkflowRules.FitsReceipt(100m,100m,0m,0m),"Zero allocation rejected");
Check(!HerbalistWorkflowRules.FitsReceipt(100m,100m,0m,-1m),"Negative allocation rejected");
Check(HerbalistWorkflowRules.FefoAvailableGrams(0.00001m)==0.01m,"Smallest batch precision converts without loss");
Check(HerbalistWorkflowRules.FefoAvailableGrams(0.123456m)==123.45m,"FEFO never rounds up available stock");
Check(HerbalistWorkflowRules.SupportsStockUnit("g"),"Database gram stock is supported");
Check(HerbalistWorkflowRules.SupportsStockUnit("kg"),"Kilogram stock remains supported");
Check(!HerbalistWorkflowRules.SupportsStockUnit("box"),"Unknown units cannot be converted");
Check(HerbalistWorkflowRules.StockToGrams(125m,"g")==125m,"Gram stock is not multiplied by 1000");
Check(HerbalistWorkflowRules.StockToGrams(0.125m,"kg")==125m,"Kilogram stock converts to grams");
Check(HerbalistWorkflowRules.GramsToStock(125m,"g")==125m,"Issue subtracts grams from gram stock");
Check(HerbalistWorkflowRules.GramsToStock(125m,"kg")==0.125m,"Issue subtracts kilograms from kilogram stock");
Check(HerbalistWorkflowRules.FefoAvailableGrams(125.129m,"g")==125.12m,"Gram FEFO rounds down to workflow precision");
Check(Valid(new HerbalistSelection([1])),"Single warehouse is resolved by API");
Check(!Valid(new HerbalistSelection([1],-1)),"Negative warehouse rejected");
Check(Valid(new HerbalistSelection([1,2],1)),"Multi-prescription request with warehouse accepted");
Check(Valid(new ReceiptLine(1,null)),"NULL receipt preserved");
Check(Valid(new ReceiptLine(1,0m)),"Checked zero preserved");
Check(!Valid(new ReceiptLine(1,-1m)),"Negative actual receipt rejected");
Check(!Valid(new DispenseAllocation(1,0m)),"Empty allocation rejected by DTO");
Check(!Valid(new CompleteHerbs("version",[])),"Completion needs source allocations");
Check(!Valid(new HerbalistSelection([],1)),"Preview needs prescriptions");
Check(Valid(new HerbalistCreateRequest([1],null)),"Creation resolves single warehouse");
Check(Valid(new HerbalistCreateRequest([1],null,0)),"Legacy zero warehouse accepted");
Check(!Valid(new HerbalistCreateRequest([1],null,-1)),"Negative creation warehouse rejected");
Check(!Valid(new HerbalistVersion("")),"Version is required");
Check(!Valid(new HerbalistReceive("version",[])),"Receive needs confirmed allocations");
Check(!Valid(new ReceiveHerbs("version",[new ReceiptLine(0,1m)],null)),"Nested receipt line is validated");
Check(!Valid(new CompleteHerbs("version",[new DispenseAllocation(1,0m)])),"Nested dispense allocation is validated");
Check(Valid(new CompleteHerbs("version",[new DispenseAllocation(1,1m)])),"Valid completion accepted");
Console.WriteLine($"PASS: {checks} Herbalist quantity and request boundary checks.");
