#r "nuget: System.Text.Encoding.CodePages"
#r "nuget: FSharp.Data"
#r "nuget: Donald"
#r "nuget: System.Data.SQLite.Core"

open System.Collections.Generic
open System.Net.Http
open System.Text
open System.IO

do Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

let url = "http://www.indexpr.moc.go.th/PRICE_PRESENT/tablecsi_month_region.asp?DDMonth=11&DDYear=2566&DDProvince=10&B1=%B5%A1%C5%A7"            
let materialsHtml = @"c:/temp/Scrape2Api/materials.html" 

let request () =
    task {
        
        use file = File.OpenWrite(materialsHtml)

        use client = new HttpClient()
        let formData = 
            [ "DDGroupCode", ""; "Submit", "%B5%A1%C5%A7" ] 
            |> Seq.map (fun (k, v) -> KeyValuePair<string, string>(k, v))
        let content = new FormUrlEncodedContent(formData)

        let! response = client.PostAsync(url, content)
        do! response.Content.CopyToAsync(file)
    }   
    |> Async.AwaitTask
    |> Async.RunSynchronously 

open System.IO
open System.Text

let materialsUtfHtml = @"c:/temp/Scrape2Api/materials-utf8.html" 

let toUTF8 sourceFilePath =
    // Read the contents of the file with the source encoding
    let sourceEncoding = Encoding.GetEncoding(874) // ISO-8859-1 encoding    
    let content = File.ReadAllText(sourceFilePath, sourceEncoding)

    // Write the contents to a new file with the target encoding
    let targetEncoding = Encoding.UTF8 // UTF-8 encoding
    File.WriteAllText(materialsUtfHtml, content, targetEncoding)

open FSharp.Data

type Material = {
    Code: string
    Description: string
    Unit: string
    UnitPrice: decimal
}

let toMaterial (tds:string seq) = 
    let getText i = tds |> Seq.item i |> fun x -> x.Trim()

    {
        Code = getText 0
        Description = getText 1
        Unit = getText 2
        UnitPrice = getText 3 |> System.Decimal.Parse  
    }

let parse () =
    let htmlStream = File.OpenRead(materialsUtfHtml)
    let trs = HtmlDocument
                                .Load(htmlStream)
                                .Descendants("tr")

    trs 
    |> Seq.map (fun tr -> tr.Descendants("td") |> Seq.map (fun td -> td.InnerText()))
    |> Seq.filter (fun tds -> tds |> Seq.length |> fun len -> len >= 4)
    |> Seq.filter (fun tds ->                 
                        match tds |> Seq.item 3 |> System.Decimal.TryParse with
                        | isDecimal, _ -> isDecimal)
    |> Seq.map toMaterial

let firstMaterial = parse () |> Seq.head

open Donald

let sql = "
    INSERT INTO material (code, description, unit, unit_price)
    VALUES (@code, @description, @unit, @unit_price)
    ON CONFLICT do UPDATE SET description = @description, unit = @unit, unit_price = @unit_price";


// Strongly typed input parameters
let param = [ 
    "code", sqlString firstMaterial.Code
    "description", sqlString firstMaterial.Description
    "unit", sqlString firstMaterial.Unit
    "unit_price", sqlDecimal firstMaterial.UnitPrice
]

open System.Data.SQLite

let conn = new SQLiteConnection(@"Data Source=c:\temp\Scrape2Api\material.db;Version=3;New=true;")


conn
|> Db.newCommand sql
|> Db.setParams param
|> Db.exec // unit

// Async
conn
|> Db.newCommand sql
|> Db.setParams param
|> Db.Async.exec // Task<unit>