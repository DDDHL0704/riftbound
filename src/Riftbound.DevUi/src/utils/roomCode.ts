// Shareable room codes use an unambiguous uppercase alphabet (no 0/O/1/I/L)
// so a player can read the code aloud to a friend without confusion.
const ROOM_CODE_ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
const ROOM_CODE_BODY_LENGTH = 6;
const ROOM_CODE_PREFIX = "RB-";

export function generateRoomCode(): string {
  const body = Array.from({ length: ROOM_CODE_BODY_LENGTH }, () =>
    ROOM_CODE_ALPHABET[Math.floor(Math.random() * ROOM_CODE_ALPHABET.length)]
  ).join("");
  return `${ROOM_CODE_PREFIX}${body}`;
}
