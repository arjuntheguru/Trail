import React, { useEffect, useState, useRef } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import { Link } from 'react-router-dom';
import { Popover, Overlay } from 'react-bootstrap';
import { getSiteWithoutPagination } from '../../store/site-actions';
import { returnLoggedInUser } from '../../store/auth-actions';
import { failed, idle, succeeded } from '../../utils/stateStatus';

const ManagerPopover = () => {

    const dispatch = useDispatch();
    const user = dispatch(returnLoggedInUser());

    const [show, setShow] = useState(false);
    const [siteData, setSiteData] = useState(user.siteId)


    const { status: siteStatus, siteInfo, recentGETMethod } = useSelector(state => state.site);

    const target = useRef(null);

    const handleClick = (event) => {
        setShow(!show);
    };

    const onChangeHandler = (e) => {
      
       setSiteData(user.siteId.filter(p => p.includes(e.target.value)))
    };

    useEffect(() => {

        if (siteStatus === idle || (siteStatus === succeeded && recentGETMethod !== "getSiteWithoutPagination")) {
            dispatch(getSiteWithoutPagination(user.companyId))
        }

    }, [dispatch, user.companyId, siteStatus, recentGETMethod])

    return (
        <div>
            <a className="nav-link" onClick={handleClick} ref={target} href="#">Complete Tasks</a>

            <Overlay
                show={show}
                target={target.current}
                placement="bottom"
                rootClose
                onHide={() => setShow(false)}
            >
                <Popover>
                    <Popover.Title className="text-center">Sites</Popover.Title>
                    <div class="form-row">
                        <input className="form-control form-control-sm m-3" type="text" placeholder="Search" onChange={onChangeHandler}/>
                    </div>       
                    <Popover.Content style={{maxHeight:"200px", overflow:"auto"}}>
                        {
                            siteData.map(item => 
                                <Link 
                                    to={`/site/${item}`} 
                                    className='nav-link'
                                    onClick={() => {setShow(!show)}}
                                >
                                    {item}
                                </Link>)
                        }
              </Popover.Content>
                </Popover>
            </Overlay>
        </div>
    );

}

export default ManagerPopover;