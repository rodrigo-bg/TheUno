from fastapi import FastAPI
from pydantic import BaseModel
import sqlite3

app = FastAPI()

DB = "theuno.db"

class Player():
    Id : int
    name : string
    date : int
