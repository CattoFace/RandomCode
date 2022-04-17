import sqlalchemy as sql
from tqdm.asyncio import tqdm
import asyncio
import aiohttp
from datetime import date, timedelta

PAGES_PER_BATCH=50
MINIMUM_LEVEL=210
CURRENT_LEVEL=275
EXP=0
CHARS_PER_PAGE=5
DAYS_TO_SAVE=1
DB = 'SageCatDB'
persNA = 0
changeNA = 0
persEU = 0
changeEU = 0
con = 0
engine = sql.create_engine(f'mysql://root:root@localhost:3306/{DB}')
meta = sql.MetaData(engine)

try:
    con = engine.connect()
    persNA = sql.Table('charPersistentNA', meta, autoload=True, autoload_with=engine)
    changeNA = sql.Table('charChangingNA', meta, autoload=True, autoload_with=engine)
    persEU = sql.Table('charPersistentEU', meta, autoload=True, autoload_with=engine)
    changeEU = sql.Table('charChangingEU', meta, autoload=True, autoload_with=engine)
except:
    engine = sql.create_engine('mysql://root:root@localhost:3306')
    engine.execute(f'CREATE DATABASE {DB}')
    engine.execute(f'USE {DB}')        
    persNA = sql.Table('charPersistentNA',meta,sql.Column('Name',sql.Unicode(12),primary_key=True),sql.Column('World',sql.String(11)),sql.Column('Job',sql.String(15)),sql.Column('ImgUrl',sql.String(298)))
    changeNA = sql.Table('charChangingNA',meta,sql.Column('Name',sql.Unicode(12)),sql.Column('Level',sql.Integer),sql.Column('Exp',sql.BigInteger),\
              sql.Column('OverallRank',sql.Integer),sql.Column('JobRank',sql.Integer),sql.Column('WorldRank',sql.Integer),sql.Column('Date',sql.Date))
    persEU = sql.Table('charPersistentEU',meta,sql.Column('Name',sql.Unicode(12),primary_key=True),sql.Column('World',sql.String(11)),sql.Column('Job',sql.String(15)),sql.Column('ImgUrl',sql.String(298)))
    changeEU = sql.Table('charChangingEU',meta,sql.Column('Name',sql.Unicode(12)),sql.Column('Level',sql.Integer),sql.Column('Exp',sql.BigInteger),\
              sql.Column('OverallRank',sql.Integer),sql.Column('JobRank',sql.Integer),sql.Column('WorldRank',sql.Integer),sql.Column('Date',sql.Date))
    meta.create_all(engine)
    con=engine.connect()

async def DownloadData(pageIndex=1,eu=0):
    query=f'https://maplestory.nexon.net/api/ranking/?id=overall&page_index={str(pageIndex)+("&region=eu" if eu else "")}&id2=legendary&rebootIndex=0'
    async with aiohttp.ClientSession() as session:
        try:
            async with session.get(query) as response:
                return await response.json()
        except:
            return -1

async def updateDBPage(page,eu):
    global CURRENT_LEVEL,EXP
    data = await DownloadData(pageIndex=(page*5+1),eu=eu)
    while data ==-1:
        tqdm.write(f'timeout downloading page #{page}, retrying')
        data = await DownloadData(pageIndex=(page*5+1))
    ch = changeEU if eu else changeNA
    pe = persEU if eu else persNA
    deletePers = sql.delete(pe).where(pe.c.Name.in_(list(map(lambda c:c['CharacterName'],data))))
    #deleteChange = sql.delete(ch).where(sql.and_(ch.c.Name.in_(list(map(lambda c:c['CharacterName'],data))),sql.or_((ch.c.Date<(date.today()-timedelta(days=DAYS_TO_SAVE))),ch.c.Date==date.today())))
    insertPers = sql.insert(pe).values(list(map(lambda c:{'Name':c['CharacterName'],'World':c['WorldName'],'Job':c['JobName'],'ImgUrl':c['CharacterImgUrl']},data)))
    insertChange = sql.insert(ch).values(list(map(lambda c:{'Name':c['CharacterName'],'Level':c['Level'],'Exp':c['Exp'],'OverallRank':c['Rank'],'Date':date.today()},data)))
    con.execute(deletePers)
    #con.execute(deleteChange)
    con.execute(insertPers)
    con.execute(insertChange)
    CURRENT_LEVEL=data[4]['Level']
    EXP=data[4]['Exp']
    

async def updateDBPageWrapper(page,bar,eu):
    await updateDBPage(page,eu)
    bar.update()
    
async def updatePages(lower,higher,eu):
    with tqdm(total=higher-lower,desc=f'Downloading Pages {lower+1}-{higher}',unit=' characters',unit_scale=CHARS_PER_PAGE) as gatherBar:
        await asyncio.gather(*[updateDBPageWrapper(i,gatherBar,eu) for i in range(lower,higher)])

async def main():
    global CURRENT_LEVEL
    with tqdm(desc='Total Progress NA',unit=' characters',unit_scale=CHARS_PER_PAGE*PAGES_PER_BATCH) as totalProgress:
        i=0
        while CURRENT_LEVEL>=MINIMUM_LEVEL:
            await updatePages(i*PAGES_PER_BATCH,(i+1)*(PAGES_PER_BATCH),0)
            totalProgress.update()
            totalProgress.set_description(f'Total Progress NA: Level:{CURRENT_LEVEL}, EXP:{EXP}, Target Level:{MINIMUM_LEVEL}')
            i+=1
    CURRENT_LEVEL=275
    with tqdm(desc='Total Progress EU',unit=' characters',unit_scale=CHARS_PER_PAGE*PAGES_PER_BATCH) as totalProgress:
        i=0
        while CURRENT_LEVEL>=MINIMUM_LEVEL:
            await updatePages(i*PAGES_PER_BATCH,(i+1)*(PAGES_PER_BATCH),1)
            totalProgress.update()
            totalProgress.set_description(f'Total Progress EU: Level:{CURRENT_LEVEL}, EXP:{EXP}, Target Level:{MINIMUM_LEVEL}')
            i+=1

asyncio.get_event_loop().run_until_complete(main())


