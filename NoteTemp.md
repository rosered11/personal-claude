# RFID

- ต้องมี epc สำหรับกล่อง และ กล่องนั้นต้องมี relation กับ epc item

- ขาเข้าต้องทำ mapping epc to po ด้วย

- ขาออกต้องทำ mapping epc to so ด้วย

- dock ไม่จำเป็นต้องมีเนื่องจากของที่ได้รับจะมากองอยู่ที่ zone เอาของเข้า แล้วจะเอาของเข้าตาม PO

- ต้องทำระบบ scan cycle count by locations ด้วย โดยจะมีตั้งต้นว่า location นี้มี epc ภายใต้ location นี้อะไรบ้าง

- อาจจะต้องทำ epc by container และใน container ต้อง relate ได้ว่า epc 

- Partial reader epc by po โดยต้องมีกระบวนการตรวจสอบว่า

- สรุป epc จาก supplier เราจะไม่สนใจ แต่เราจะ set prefix เองตาม supplier เพื่อให้มั่นใจว่าไม่มีทางชนกัน