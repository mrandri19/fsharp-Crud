#r "Suave.1.1.2/lib/net40/Suave.dll";;
open System
open Suave
open Suave.Successful
open Suave.Operators
open Suave.Filters
open Suave.RequestErrors
open Suave.Cookie

type User = { username: string; password: string }

let Andrea = { User.username = "andrea"; password = "daw" }
let Admin = { User.username = "admin"; password = "admin" }

let userDB : User list = [Andrea; Admin]

let main =
    let serveStatic filename = Files.file filename

    let welcomePage = "home.html"
    let loginPage = "login.html"

    let getUsernameAndPassword (r:HttpRequest) =
        let username = match r.formData "username" with
                       | Choice1Of2 c -> c
                       | Choice2Of2 c -> c

        let password = match r.formData "password" with
                       | Choice1Of2 c -> c
                       | Choice2Of2 c -> c
        (username, password)

    let loginSuccessful username password =
        (sprintf "Welcome\n user: %s\npassword: %s\n" username password) |> OK
    let loginFailed = OK "Login failed"

    let setLoggedCookie =
        setCookie { HttpCookie.name = "auth";
                    value = "true";
                    expires = None;
                    path = Some "/";
                    domain = None;
                    secure = false;
                    httpOnly = false }
        >=> Redirection.redirect "/"

    let loginHandler (r:HttpRequest) =
        let (username, password) = getUsernameAndPassword r
        // hash password, search for username in db, if pw == dbPw then setcookie "authenticated"
        // else print "no user n"
        match List.tryFind (fun user -> user = {User.username = username; password = password}) userDB with
            | Some(user) -> setLoggedCookie
            | None -> loginFailed


    let isAuthenticated (r:HttpRequest) (x: HttpContext) = match Map.tryFind "auth" r.cookies with
                                                           | Some(_) -> async.Return (Some x)
                                                           | None -> async.Return (None)

    let authMiddleware = request isAuthenticated

    let app = choose [
                path "/" >=> serveStatic welcomePage
                pathScan "/static/%s" serveStatic
                path "/login" >=> choose [
                                    POST >=> request loginHandler
                                    GET >=> serveStatic loginPage
                                    ]
                path "/logout" >=> choose [
                                    authMiddleware >=> OK "we need to log you out, you are authenticated"
                                    OK "you are not authenticated you cant logout"
                                    ]
                path "/protected" >=> authMiddleware >=> OK "Protected"
                path "/cookies" >=> request (fun req -> OK (sprintf "%A" req.cookies) )
                NOT_FOUND "File not found"
                ]

    let config = { defaultConfig with
                     bindings = [Http.HttpBinding.mkSimple HTTP "127.0.0.1" 8000 ]
                   }
    startWebServer config app
