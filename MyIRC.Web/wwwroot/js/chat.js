function getUserNick(user) {
    return user.nick || user.Nick || "";
}

function getRoleInfo(user, channelName) {
    const nick = getUserNick(user);

    const roleValue =
        user.channelRoles?.[channelName]
        ?? user.ChannelRoles?.[channelName]
        ?? 0;

    const roleMap = {
        70: { prefix: ".", className: "role-founder", order: 1 },
        60: { prefix: "~", className: "role-owner", order: 2 },
        50: { prefix: "&", className: "role-sop", order: 3 },
        40: { prefix: "@", className: "role-op", order: 4 },
        30: { prefix: "!", className: "role-vip", order: 5 },
        20: { prefix: "%", className: "role-halfop", order: 6 },
        10: { prefix: "+", className: "role-voice", order: 7 },
        0: { prefix: "", className: "role-user", order: 8 }
    };

    if (nick.startsWith("RSohbet") && roleValue === 0) {
        return { prefix: "", className: "role-guest", order: 9 };
    }

    return roleMap[roleValue] || roleMap[0];
}

function getGroupName(roleInfo) {
    switch (roleInfo.order) {
        case 1: return "FOUNDERS";
        case 2: return "OWNERS";
        case 3: return "SOP";
        case 4: return "OP";
        case 5: return "VIP";
        case 6: return "HALFOP";
        case 7: return "VOICE";
        case 9: return "GUESTS";
        default: return "USERS";
    }
}

let virtualUserRows = [];
let userListVirtualReady = false;
let activeUserMenuNick = null;

const USER_ROW_HEIGHT = 20;
const USER_HEADER_HEIGHT = 22;
const USER_OVERSCAN = 8;

function closeUserContextMenu() {
    document.getElementById("userContextMenu")?.remove();
    activeUserMenuNick = null;
}

function openUserContextMenu(nick, anchorEl) {
    closeUserContextMenu();

    activeUserMenuNick = nick;

    const rect = anchorEl.getBoundingClientRect();

    const menu = document.createElement("div");
    menu.id = "userContextMenu";
    menu.className = "user-context-menu";

    menu.innerHTML = `
        <button class="pm-start-btn">Özel sohbet et</button>
        <button class="profile-btn">Profil</button>
        <button class="block-btn">Engelle</button>
    `;

    menu.style.left = `${rect.left}px`;
    menu.style.top = `${rect.bottom + 4}px`;

    document.body.appendChild(menu);

    menu.querySelector(".pm-start-btn")?.addEventListener("click", (e) => {
        e.stopPropagation();
        closeUserContextMenu();

        if (typeof openPrivate === "function") {
            openPrivate(nick);
        }
    });

    menu.querySelector(".profile-btn")?.addEventListener("click", (e) => {
        e.stopPropagation();
        console.log("Profil:", nick);
    });

    menu.querySelector(".block-btn")?.addEventListener("click", (e) => {
        e.stopPropagation();
        console.log("Engelle:", nick);
    });
}

document.addEventListener("pointerdown", (e) => {
    const menu = document.getElementById("userContextMenu");

    if (menu && menu.contains(e.target)) {
        return;
    }

    if (e.target.closest(".user-item")) {
        return;
    }

    closeUserContextMenu();

    document.querySelectorAll(".user-item").forEach(i => {
        i.classList.remove("active");
    });
});

function buildVirtualUserRows() {
    const grouped = {};

    allUsers
        .filter(user =>
            user.channels &&
            user.channels.some(c => c.toLowerCase() === currentChannel.toLowerCase())
        )
        .forEach(user => {
            const roleInfo = getRoleInfo(user, currentChannel);
            const key = roleInfo.order;

            if (!grouped[key]) grouped[key] = [];
            grouped[key].push(user);
        });

    const rows = [];
    let top = 0;

    Object.keys(grouped)
        .sort((a, b) => Number(a) - Number(b))
        .forEach(order => {
            const users = grouped[order];

            users.sort((a, b) =>
                getUserNick(a).localeCompare(getUserNick(b), "tr")
            );

            const roleInfo = getRoleInfo(users[0], currentChannel);

            rows.push({
                type: "header",
                text: `${getGroupName(roleInfo)} (${users.length})`,
                top,
                height: USER_HEADER_HEIGHT
            });

            top += USER_HEADER_HEIGHT;

            users.forEach(user => {
                rows.push({
                    type: "user",
                    user,
                    top,
                    height: USER_ROW_HEIGHT
                });

                top += USER_ROW_HEIGHT;
            });
        });

    virtualUserRows = rows;
    return top;
}

function initVirtualUserList() {
    if (userListVirtualReady) return;

    const usersList = document.getElementById("usersList");
    if (!usersList) return;

    usersList.addEventListener("scroll", () => {
        closeUserContextMenu();
        renderVirtualUserRows();
    });

    userListVirtualReady = true;
}

function renderUserList() {
    const usersList = document.getElementById("usersList");
    if (!usersList) return;

    closeUserContextMenu();
    initVirtualUserList();

    const totalHeight = buildVirtualUserRows();

    usersList.innerHTML = `
        <div class="virtual-user-spacer" style="height:${totalHeight}px;"></div>
    `;

    renderVirtualUserRows();
}

function renderVirtualUserRows() {
    const usersList = document.getElementById("usersList");
    const spacer = usersList?.querySelector(".virtual-user-spacer");

    if (!usersList || !spacer) return;

    spacer.innerHTML = "";

    const scrollTop = usersList.scrollTop;
    const viewHeight = usersList.clientHeight;

    const visibleRows = virtualUserRows.filter(row =>
        row.top + row.height >= scrollTop - USER_OVERSCAN * USER_ROW_HEIGHT &&
        row.top <= scrollTop + viewHeight + USER_OVERSCAN * USER_ROW_HEIGHT
    );

    visibleRows.forEach(row => {
        if (row.type === "header") {
            const header = document.createElement("div");
            header.className = "user-group-header virtual-user-row";
            header.style.transform = `translateY(${row.top}px)`;
            header.style.height = `${row.height}px`;
            header.textContent = row.text;

            spacer.appendChild(header);
            return;
        }

        const user = row.user;
        const nick = getUserNick(user);
        const roleInfo = getRoleInfo(user, currentChannel);

        const item = document.createElement("div");

        item.className = nick === currentUser
            ? `user-item self ${roleInfo.className} virtual-user-row`
            : `user-item ${roleInfo.className} virtual-user-row`;

        item.style.transform = `translateY(${row.top}px)`;
        item.style.height = `${row.height}px`;

        item.innerHTML = `
            <span class="prefix">${roleInfo.prefix}</span>
    <span class="nick" title="${nick}">${nick}</span>
        `;

        item.addEventListener("click", (e) => {
            e.stopPropagation();

            document.querySelectorAll(".user-item").forEach(i => {
                if (i !== item) i.classList.remove("active");
            });

            item.classList.add("active");

            openUserContextMenu(nick, item);
        });

        spacer.appendChild(item);
    });
}
