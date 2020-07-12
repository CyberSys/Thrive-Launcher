// This file is required by the index.html file and will
// be executed in the renderer process for that window.
// All of the Node.js APIs are available in this process.
"use strict";

const assert = require("assert");

const versionInfo = require("./version_info");
const retrieveNews = require("./retrieve_news");

const {Modal, showGenericError} = require("./modal");
const autoUpdateHandler = require("./src/auto_update_handler");
const {checkConnectionStatus} = require("./src/dev_center");
const {sendVersionInfoToPlayButton, playCallback} = require("./src/version_select_button");
const {checkIfCompatible, performCompatibilityCheck} =
    require("./src/compatibility_check");
const {getLauncherKey} = require("./src/launcher_key");
const {loadVersionData} = require("./src/version_info_retriever");
const {playPressed} = require("./src/play_handler");

const openpgp = require("openpgp");

const titleBar = require("./title_bar");
titleBar.loadTitleBar();

autoUpdateHandler();

//
// Settings thing
//
const {settings, loadSettings} = require("./settings.js");

// This loads settings in sync mode here
loadSettings();

// Start checking DevCenter token
checkConnectionStatus();

// Start checking hardware
checkIfCompatible();

// Some other variables

const linksModal = new Modal("linksModal", "linksModalDialog", {closeButton: "linksClose"});

const constOldVersionErrorModal = new Modal("errorOldCantStartModal",
    "errorOldCantStartModalDialog", {autoClose: false});

// Parses version information from data and adds it to all the places
function onVersionDataReceived(data, unsigned = false){
    // Check launcher version //
    new Promise(function(resolve, reject){
        if(unsigned){
            versionInfo.parseData(data);
            resolve();
            return;
        }

        getLauncherKey().then((key) => {
            if(!key){

                reject(new Error("get launcher key is null"));
                return;
            }

            // Unpack and verify signature //
            openpgp.cleartext.readArmored(data).then((message) => {

                const options = {
                    message: message,
                    publicKeys: key,
                };

                openpgp.verify(options).then(function(verified){
                    const validity = verified.signatures[0].valid;

                    if(validity){
                        console.log("Version data signed by key id " +
                            verified.signatures[0].keyid.toHex());

                        versionInfo.parseData(verified.data);

                        resolve();

                    } else {
                        const msg = "Error verifying signature validity. " +
                            "Did the download get corrupted?";
                        showGenericError(msg, () => {

                            reject(msg);
                        });
                    }
                });

            }, (err) => {
                reject(err);
            });
        }, (err) => {
            reject(err);
        });
    }).then(() => {
        return checkLauncherVersion(versionInfo);
    }).then(() => {

        sendVersionInfoToPlayButton(versionInfo);

    }).catch((err) => {
        // Fail //
        constOldVersionErrorModal.show();

        if(err){

            const text = document.getElementById("errorOldCantStartText");

            if(text){
                text.append(document.createElement("br"));
                text.append(document.createTextNode(" Error message: " + err));
            }
        }
    });
}

// Maybe this does something to the stuck downloading version info bug
// TODO: switch the callback to use a promise here
loadVersionData(onVersionDataReceived);

playCallback(() => {
    performCompatibilityCheck(playPressed);
});

const newsContent = document.getElementById("newsContent");

const devForumPosts = document.getElementById("devForumPosts");

//
// Starts loading the news and shows them once loaded
//
function loadNews(){

    retrieveNews.retrieveNews(function(news, devposts){

        assert(news);
        assert(devposts);

        if(news.error){

            newsContent.textContent = news.error;

        } else {

            assert(news.htmlNodes);

            newsContent.innerHTML = "";
            newsContent.append(news.htmlNodes);
        }

        if(devposts.error){

            devForumPosts.textContent = devposts.error;

        } else {

            assert(devposts.htmlNodes);

            devForumPosts.innerHTML = "";
            devForumPosts.append(devposts.htmlNodes);
        }

    });
}

// Clear news and start loading them

if(settings.fetchNewsFromWeb){

    newsContent.textContent = "Loading...";
    devForumPosts.textContent = "Loading...";

    loadNews();

} else {

    newsContent.textContent = "Web content is disabled.";
    devForumPosts.textContent = "Web content is disabled.";
}


// Links button
const linksButton = document.getElementById("linksButton");

linksButton.addEventListener("click", function(){

    linksModal.show();
});

// Settings dialog
require("./settings_dialog.js");
const {checkLauncherVersion} = require("./src/check_launcher_version");
