"use strict";

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

connection.on("AskAnnounce", function (message) {
  $('#announceModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("AskGameType", function (message) {
  $('#announceGameTypeModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("AskColor", function (message) {
  $('#gameColorModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("AskWantToPlay", function () {
  $('#gameOverModal').modal('hide');
  $('#wantToPlayModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("GameOver", function (title, body) {
  $('#announceModal').modal('hide');
  $('#announceGameTypeModal').modal('hide');
  $('#wantToPlayModal').modal('hide');
  $('#gameColorModal').modal('hide');
  document.getElementById("gameOverModalTitle").textContent = title;
  document.getElementById("gameOverModalBody").textContent = body;
  $('#gameOverModal').modal({ keyboard: false, backdrop: "static" });
});

connection.on("StoreUserId", function (id) {
  localStorage.setItem("userId", id);
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

connection.on("ReceiveTrick", function (cards) {
    document.getElementById("card-bottom").src = cards[0] != "" ? `/carddecks/noto/${cards[0]}.svg` : "";
    document.getElementById("card-left").src = cards[1] != "" ? `/carddecks/noto/${cards[1]}.svg` : "";
    document.getElementById("card-top").src = cards[2] != "" ? `/carddecks/noto/${cards[2]}.svg` : "";
    document.getElementById("card-right").src = cards[3] != "" ? `/carddecks/noto/${cards[3]}.svg` : "";
});

connection
  .start()
  .then(function () {
    document.getElementById("sendButton").disabled = false;
    // The following code allows a user to reconnect after reloading the page or restarting the browser
    // During development this is not useful as it is more difficult to simulate multiple users from one machine
    // var userId = localStorage.getItem("userId");
    // if (userId) {
    //   connection
    //     .invoke("ReconnectPlayer", userId)
    //     .catch(function (err) {
    //       $('#usernameModal').modal({ keyboard: false, backdrop: "static" });
    //       return console.error(err.toString());
    //     });
    // } else {
      $('#usernameModal').modal({ keyboard: false, backdrop: "static" });
    // }
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
    var userName = document.getElementById("startModalUserName").value;
    connection
      .invoke("AddPlayer", userName)
      .catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("restartButton")
  .addEventListener("click", function (event) {
    connection
      .invoke("ResetGame",).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });