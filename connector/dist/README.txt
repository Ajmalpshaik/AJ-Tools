AJ TOOLS CONNECTOR
==================

What this is
------------
A small add-in that puts AJ Tools inside your web browser and runs them on the
Revit model you have open.

You install this once. After that, when Ajmal publishes a new tool it appears
for you on its own. You never install anything again.


How to install
--------------
1. Extract this whole zip to a folder. Do not run it from inside the zip.
2. Close Revit if it is open.
3. Double-click  Install.cmd
4. Start Revit and open any model.
5. Go to the "AJ Connector" tab and click "Open Panel".

Your browser opens with the tools. Click one, and it works on your model.
The answer appears on the web page, not as a popup inside Revit.

No admin rights are needed. Nothing is added to Program Files, and Windows
will not ask you about the firewall.


Is this safe?
-------------
Yes, and here is exactly why, in plain terms.

The tools are not stored inside this add-in. They are downloaded. That would
normally be risky, so every tool carries Ajmal's digital signature, and the
connector checks that signature before running anything.

If a tool is not signed by Ajmal, or if even one character of it was changed
after he published it, the connector refuses it. It is not shown to you, and
it cannot be run. Refused tools are only ever reported as a count.

The web page only works on your own computer. Nobody on your network or on the
internet can reach it, and it only runs while you have the panel open.


If something does not work
--------------------------
"No AJ Connector tab in Revit"
    Revit was probably open during the install. Close Revit completely and
    start it again.

"The browser says it cannot reach the page"
    The address needs the port number on the end, for example
    http://localhost:48230/ - just typing "localhost" will not work. Easiest
    fix: click "Open Panel" in Revit again and let it open the browser for you.

"No tools published yet"
    That is not an error. The connector is working and talking to Revit, there
    just are not any tools published to it yet.

"Some tools were refused"
    The connector is doing its job. Those tools were not signed by Ajmal or
    were altered. Tell Ajmal - do not try to bypass it.


To remove it
------------
Run this in the same folder:

    powershell -ExecutionPolicy Bypass -File install-connector.ps1 -Uninstall

It removes only the connector. Any other add-ins you have, including AJ Tools
itself, are left alone.


Created & All Rights Reserved @ Ajmal P.S.
