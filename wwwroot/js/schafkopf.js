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

connection.on("ReceiveHand", function (cards) {
  var hand = document.getElementById("hand");
  hand.innerHTML = "";
  for (const cardName of cards) {
    var card = document.createElement("img");
    card.src = `/carddecks/noto/${cardName}.svg`;
    card.style = "width: 12.5%;";
    card.id = cardName;
    card.addEventListener("click", function (event) {
      var user = document.getElementById("userInput").value;
      connection
        .invoke("SendChatMessage", user, event.srcElement.id)
        .catch(function (err) {
          return console.error(err.toString());
        });
      event.preventDefault();
    });
    hand.appendChild(card);
  }
});

connection
  .start()
  .then(function () {
    document.getElementById("sendButton").disabled = false;
  })
  .catch(function (err) {
    return console.error(err.toString());
  });

document
  .getElementById("sendButton")
  .addEventListener("click", function (event) {
    var user = document.getElementById("userInput").value;
    var message = document.getElementById("messageInput").value;
    connection.invoke("SendChatMessage", user, message).catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("dealCardsButton")
  .addEventListener("click", function (event) {
    connection.invoke("DealCards").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });
