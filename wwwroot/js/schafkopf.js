"use strict";

function tryReconnect() {
  // The following code allows a user to reconnect after reloading the page or restarting the browser
  // During development this is not useful as it is more difficult to simulate multiple users from one machine
  // append `?session=new` to the url to force a new connection
  let searchParams = new URLSearchParams(window.location.search);
  var userId = localStorage.getItem("userId");
  if (userId && !(searchParams.get("session") == "new")) {
    connection
      .invoke("ReconnectPlayer", userId, searchParams.get("game"))
      .catch(function (err) {
        return console.error(err.toString());
      });
  } else {
    $('#usernameModal').modal({ keyboard: false, backdrop: "static" });
  }
}

function setTheme(theme) {
  localStorage.setItem("theme", theme);
  var button = document.getElementById("toggleThemeButton");
  var body = document.getElementsByTagName("body")[0];
  if (theme == "Dark") {
    button.textContent = "Light";
    body.classList.add("bg-dark");
    body.classList.add("text-white");
    body.classList.remove("bg-white");
    body.classList.remove("text-dark");
  } else {
    button.textContent = "Dark";
    body.classList.add("bg-white");
    body.classList.add("text-dark");
    body.classList.remove("bg-dark");
    body.classList.remove("text-white");
  }
}

try {
  setTheme(localStorage.getItem("theme"));
} catch { }

var connection = new signalR.HubConnectionBuilder()
  .withUrl("/schafkopfHub")
  .build();

//Disable send button until connection is established
document.getElementById("sendButton").disabled = true;

connection.on("ReceiveChatMessage", function (user, message) {
  var div = document.createElement("div");
  var userB = div.appendChild(document.createElement("b"));
  var msgSpan = div.appendChild(document.createElement("span"));
  userB.textContent = `${user}: `;
  msgSpan.textContent = message;
  document.getElementById("messagesList").appendChild(div);
  var messageList = document.getElementById("messagesList");
  messageList.scrollTop = messageList.scrollHeight;
});

connection.on("ReceiveSystemMessage", function (message) {
  var div = document.createElement("div");
  var userB = div.appendChild(document.createElement("b"));
  var msgSpan = div.appendChild(document.createElement("span"));
  userB.textContent = "System: ";
  msgSpan.textContent = message;
  document.getElementById("messagesList").appendChild(div);
  var messageList = document.getElementById("messagesList");
  messageList.scrollTop = messageList.scrollHeight;
});

connection.on("AskUsername", function (message) {
  localStorage.removeItem("userId");
  $('#usernameModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("GameOver", function (title, body) {
  document.getElementById("gameOverModalTitle").textContent = title;
  document.getElementById("gameOverModalBody").textContent = body;
  $('#gameOverModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("AskAnnounce", function (message) {
  document.getElementById("announceModalTitle").textContent = "Magst du spielen?";
  $('#announceModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("AskGameType", function (message) {
  $('#announceGameTypeModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("AskColor", function (message) {
  $('#gameColorModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("AskWantToPlay", function () {
  $('#wantToPlayModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("AskAnnounceHochzeit", function (message) {
  document.getElementById("announceModalTitle").textContent = "Magst du eine Hochzeit anbieten?";
  $('#announceModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("AskWantToMarryPlayer", function (message) {
  document.getElementById("announceModalTitle").textContent = `Magst du ${message} heiraten?`;
  $('#announceModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("AskWantToSpectate", function (players) {
  document.getElementById("wantToSpectatePlayer1Button").textContent = players[0];
  document.getElementById("wantToSpectatePlayer2Button").textContent = players[1];
  document.getElementById("wantToSpectatePlayer3Button").textContent = players[2];
  document.getElementById("wantToSpectatePlayer4Button").textContent = players[3];
  $('#wantToSpectateModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("AskAllowSpectator", function (player) {
  document.getElementById("allowSpectatorModalTitle").textContent = `Erlaubst du ${player} bei dir zuzuschauen?`;
  $('#allowSpectatorModal').modal({ keyboard: false, backdrop: "static" });
})

connection.on("CloseGameOverModal", function () {
  $('#gameOverModal').modal('hide');
});

connection.on("CloseAnnounceModal", function () {
  $('#announceModal').modal('hide');
});

connection.on("CloseAnnounceGameTypeModal", function () {
  $('#announceGameTypeModal').modal('hide');
});

connection.on("CloseGameColorModal", function () {
  $('#gameColorModal').modal('hide');
});

connection.on("CloseWantToPlayModal", function () {
  $('#wantToPlayModal').modal('hide');
});

connection.on("CloseWantToSpectateModal", function () {
  $('#wantToSpectateModal').modal('hide');
});

connection.on("CloseAllowSpectatorModal", function (player) {
  $('#allowSpectatorModal').modal('hide');
})

connection.on("StoreUserId", function (id) {
  localStorage.setItem("userId", id);
  $('#usernameModal').modal('hide');
});

connection.on("ReceiveHand", function (cards) {
  var hand = document.getElementById("hand");
  hand.innerHTML = "";
  for (const cardName of cards) {
    var card = document.createElement("img");
    card.src = `/carddecks/noto/${cardName}.svg`;
    card.style = "width: 12.5%;";
    card.id = cardName;
    card.addEventListener("click", function (event) {
      connection
        .invoke("PlayCard", event.srcElement.id)
        .catch(function (err) {
          return console.error(err.toString());
        });
      event.preventDefault();
    });
    hand.appendChild(card);
  }
});

connection.on("ReceivePlayers", function (players) {
  document.getElementById("player-bottom").textContent = players[0];
  document.getElementById("player-left").textContent = players[1];
  document.getElementById("player-top").textContent = players[2];
  document.getElementById("player-right").textContent = players[3];
});

connection.on("ReceiveTrick", function (cards) {
  document.getElementById("card-bottom").src = cards[0] != "" ? `/carddecks/noto/${cards[0]}.svg` : "/carddecks/blank.svg";
  document.getElementById("card-left").src = cards[1] != "" ? `/carddecks/noto/${cards[1]}.svg` : "/carddecks/blank.svg";
  document.getElementById("card-top").src = cards[2] != "" ? `/carddecks/noto/${cards[2]}.svg` : "/carddecks/blank.svg";
  document.getElementById("card-right").src = cards[3] != "" ? `/carddecks/noto/${cards[3]}.svg` : "/carddecks/blank.svg";
});

connection.on("ReceiveLastTrickButton", function (buttonState) {
  switch (buttonState) {
    case "disabled":
      document.getElementById("toggleLastTrickButton").textContent = "";
      break;
    case "show":
      document.getElementById("toggleLastTrickButton").textContent = "Letzten Stich zeigen";
      break;
    case "hide":
      document.getElementById("toggleLastTrickButton").textContent = "Letzten Stich verstecken";
      break;
  }
});

connection
  .start()
  .then(function () {
    document.getElementById("sendButton").disabled = false;
    let searchParams = new URLSearchParams(window.location.search);
    if (!searchParams.get("game")) {
      $('#gameIdModal').modal({ keyboard: false, backdrop: "static" });
      return;
    }
    tryReconnect();
  })
  .catch(function (err) {
    return console.error(err.toString());
  });

document
  .getElementById("sendButton")
  .addEventListener("click", function (event) {
    var message = document.getElementById("messageInput").value;
    connection.invoke("SendChatMessage", message).catch(function (err) {
      return console.error(err.toString());
    });
    document.getElementById("messageInput").value = "";
    event.preventDefault();
  });

document
  .getElementById("wantToPlayButton")
  .addEventListener("click", function (event) {
    connection.invoke("PlayerWantsToPlay").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("wantToPauseButton")
  .addEventListener("click", function (event) {
    connection.invoke("PlayerWantsToPause").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceNoButton")
  .addEventListener("click", function (event) {
    connection.invoke("Announce", false).catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceYesButton")
  .addEventListener("click", function (event) {
    connection.invoke("Announce", true).catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceSauspielButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Sauspiel").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceWenzButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Wenz").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceSoloButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Solo").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("eichelButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameColor", "Eichel").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("grasButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameColor", "Gras").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("herzButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameColor", "Herz").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("schellenButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameColor", "Schellen").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("startButton")
  .addEventListener("click", function (event) {
    let searchParams = new URLSearchParams(window.location.search);
    var userName = document.getElementById("startModalUserName").value;
    connection
      .invoke("AddPlayer", userName, searchParams.get("game"))
      .catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("restartButton")
  .addEventListener("click", function (event) {
    connection
      .invoke("ResetGame").catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("doNotSpectateButton")
  .addEventListener("click", function (event) {
    connection
      .invoke("PlayerWantsToSpectate", -1).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("wantToSpectatePlayer1Button")
  .addEventListener("click", function (event) {
    connection
      .invoke("PlayerWantsToSpectate", 0).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("wantToSpectatePlayer2Button")
  .addEventListener("click", function (event) {
    connection
      .invoke("PlayerWantsToSpectate", 1).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("wantToSpectatePlayer3Button")
  .addEventListener("click", function (event) {
    connection
      .invoke("PlayerWantsToSpectate", 2).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("wantToSpectatePlayer4Button")
  .addEventListener("click", function (event) {
    connection
      .invoke("PlayerWantsToSpectate", 3).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("doNotAllowSpectatorButton")
  .addEventListener("click", function (event) {
    connection
      .invoke("AllowSpectator", false).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("allowSpectatorButton")
  .addEventListener("click", function (event) {
    connection
      .invoke("AllowSpectator", true).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("gameIdSubmitButton")
  .addEventListener("click", function (event) {
    $('#gameIdModal').modal('hide');
    let searchParams = new URLSearchParams(window.location.search);
    searchParams.set("game", document.getElementById("gameIdInput").value)
    window.location.search = searchParams.toString();
    tryReconnect();
    event.preventDefault();
  });

document
  .getElementById("toggleLastTrickButton")
  .addEventListener("click", function (event) {
    if (document.getElementById("toggleLastTrickButton").textContent.trim() == "Letzten Stich verstecken") {
      connection
        .invoke("ShowLastTrick", false).catch(function (err) {
          return console.error(err.toString());
        });
    } else if (document.getElementById("toggleLastTrickButton").textContent.trim() == "Letzten Stich zeigen") {
      connection
        .invoke("ShowLastTrick", true).catch(function (err) {
          return console.error(err.toString());
        });
    }
    event.preventDefault();
  });

document
  .getElementById("toggleThemeButton")
  .addEventListener("click", function (event) {
    var button = document.getElementById("toggleThemeButton");
    setTheme(button.textContent.trim());
    event.preventDefault();
  });